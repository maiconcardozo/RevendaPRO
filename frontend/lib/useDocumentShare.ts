"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import {
  isHandheld,
  openPendingWindow,
  prepareDocument,
  sendDocument,
  sendThroughPendingWindow,
  type PreparedDocument,
  type ShareResult,
} from "./share";

type Prepared = { token: string; document: PreparedDocument };

/**
 * O botão "Mandar pelo WhatsApp" com o toque preservado (M20, corrigido depois do teste na
 * loja).
 *
 * A folha do aparelho e a aba nova só abrem **dentro do toque**, e buscar o PDF leva um segundo.
 * Este hook resolve isso de dois jeitos:
 *
 * - com `prefetch`, o PDF é buscado assim que a tela abre, e o toque já o encontra pronto —
 *   é o caso da ficha do carro, onde há um PDF só e a pessoa provavelmente vai mandá-lo;
 * - sem `prefetch`, o primeiro toque busca. No computador, a aba do WhatsApp é aberta em branco
 *   ainda no toque e apontada quando o PDF chega, então continua sendo um toque só. No celular
 *   a folha precisa de um toque novo, e a tela diz isso — dois toques honestos valem mais do que
 *   um que falha em silêncio.
 *
 * `key` invalida o que foi buscado quando o que vai no PDF muda — uma foto nova, um preço
 * editado. O PDF guardado leva junto o token para o qual foi buscado, e só vale enquanto ele
 * for o atual: assim nada precisa ser zerado dentro de um efeito.
 */
export function useDocumentShare({
  path,
  fallbackName,
  prefetch = false,
  key,
}: {
  path: string;
  fallbackName: string;
  prefetch?: boolean;
  key?: unknown;
}) {
  const token = JSON.stringify([path, fallbackName, key ?? null]);

  const [stored, setStored] = useState<Prepared | null>(null);
  const [preparing, setPreparing] = useState(false);
  const inFlight = useRef<{ token: string; promise: Promise<PreparedDocument | null> } | null>(null);

  const prepared = stored !== null && stored.token === token ? stored.document : null;

  const prepare = useCallback((): Promise<PreparedDocument | null> => {
    if (inFlight.current?.token === token) return inFlight.current.promise;

    setPreparing(true);

    const promise = prepareDocument(path, fallbackName).then((result) => {
      if (inFlight.current?.token === token) inFlight.current = null;

      setPreparing(false);

      if (!result.ok) return null;

      setStored({ token, document: result.document });

      return result.document;
    });

    inFlight.current = { token, promise };

    return promise;
  }, [path, fallbackName, token]);

  useEffect(() => {
    if (!prefetch) return;

    let alive = true;

    prepareDocument(path, fallbackName).then((result) => {
      if (alive && result.ok) setStored({ token, document: result.document });
    });

    return () => {
      alive = false;
    };
  }, [path, fallbackName, prefetch, token]);

  /** Chame direto do `onClick`, sem `await` antes. */
  const send = useCallback(
    async (message: string, phone?: string | null): Promise<ShareResult> => {
      if (prepared) {
        return sendDocument(prepared, message, phone);
      }

      if (isHandheld()) {
        // A folha precisa de um toque novo depois que o PDF chegar.
        const document = await prepare();

        return document
          ? { ok: true, how: "preparing" }
          : { ok: false, error: "Falha ao gerar o PDF." };
      }

      // Ainda no toque: a aba abre em branco agora e é apontada quando o PDF chegar.
      const popup = openPendingWindow();
      const document = await prepare();

      if (!document) {
        popup?.close();
        return { ok: false, error: "Falha ao gerar o PDF." };
      }

      return sendThroughPendingWindow(popup, document, message, phone);
    },
    [prepared, prepare],
  );

  return { send, ready: prepared !== null, preparing };
}
