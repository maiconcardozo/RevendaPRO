"use client";

import { useEffect, useRef, useState } from "react";
import { Modal } from "@/components/common/Modal";

/**
 * A proporção da moldura: a mesma do espaço que o logotipo ocupa no timbre. Larga, porque um
 * logotipo deitado ao lado do nome da revenda é como a maioria dos papéis timbrados se desenha
 * — e um logotipo quadrado cabe dentro dela com ar dos lados.
 */
export const LOGO_RATIO = 2;

/** O que sobe: 1200 × 600, o dobro do que o servidor guarda, para a redução ficar nítida. */
const OUTPUT_WIDTH = 1200;

/**
 * O recorte do logotipo, no navegador (M25).
 *
 * A moldura tem a proporção do papel; a pessoa arrasta a imagem e aproxima até caber. O que ela
 * vê aqui é exatamente o que sai impresso — e o servidor recebe um PNG já certo, em vez de ter
 * de adivinhar onde cortar uma imagem que ele nunca viu inteira.
 *
 * Sem biblioteca: é um `canvas`, uma escala e um deslocamento. O fundo fica transparente, então
 * o logotipo quadrado que sobra ar dos lados chega ao papel sem retângulo em volta.
 */
export function LogoCropper({
  file,
  onCancel,
  onCropped,
  busy,
  error,
}: {
  file: File;
  onCancel: () => void;
  /** O PNG recortado, pronto para subir. */
  onCropped: (blob: Blob) => void;
  busy: boolean;
  error: string;
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [image, setImage] = useState<HTMLImageElement | null>(null);
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const dragging = useRef<{ x: number; y: number; ox: number; oy: number } | null>(null);

  // A moldura em pixels de tela.
  const frame = { width: 560, height: 560 / LOGO_RATIO };

  useEffect(() => {
    const url = URL.createObjectURL(file);
    const img = new Image();

    img.onload = () => {
      setImage(img);
      setZoom(1);
      setOffset({ x: 0, y: 0 });
    };

    img.src = url;

    return () => URL.revokeObjectURL(url);
  }, [file]);

  /** A escala em que a imagem inteira cabe na moldura: o ponto de partida, e o mínimo. */
  function fitScale(img: HTMLImageElement) {
    return Math.min(frame.width / img.width, frame.height / img.height);
  }

  /** Onde a imagem é desenhada, em pixels de tela, para a escala e o deslocamento atuais. */
  function placement(img: HTMLImageElement) {
    const scale = fitScale(img) * zoom;
    const width = img.width * scale;
    const height = img.height * scale;
    const x = (frame.width - width) / 2 + offset.x;
    const y = (frame.height - height) / 2 + offset.y;

    return { x, y, width, height };
  }

  useEffect(() => {
    const canvas = canvasRef.current;

    if (!canvas || !image) return;

    const context = canvas.getContext("2d");

    if (!context) return;

    context.clearRect(0, 0, canvas.width, canvas.height);

    const { x, y, width, height } = placement(image);

    context.drawImage(image, x, y, width, height);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [image, zoom, offset]);

  function onPointerDown(event: React.PointerEvent<HTMLCanvasElement>) {
    dragging.current = { x: event.clientX, y: event.clientY, ox: offset.x, oy: offset.y };
    event.currentTarget.setPointerCapture(event.pointerId);
  }

  function onPointerMove(event: React.PointerEvent<HTMLCanvasElement>) {
    const start = dragging.current;

    if (!start) return;

    setOffset({
      x: start.ox + (event.clientX - start.x),
      y: start.oy + (event.clientY - start.y),
    });
  }

  function onPointerUp() {
    dragging.current = null;
  }

  function crop() {
    if (!image) return;

    const out = document.createElement("canvas");
    out.width = OUTPUT_WIDTH;
    out.height = OUTPUT_WIDTH / LOGO_RATIO;

    const context = out.getContext("2d");

    if (!context) return;

    // O mesmo enquadramento da prévia, na escala da saída.
    const factor = OUTPUT_WIDTH / frame.width;
    const { x, y, width, height } = placement(image);

    context.drawImage(image, x * factor, y * factor, width * factor, height * factor);

    out.toBlob((blob) => {
      if (blob) onCropped(blob);
    }, "image/png");
  }

  return (
    <Modal
      title="Enquadrar o logotipo"
      onClose={onCancel}
      error={error}
      width="max-w-2xl"
      footer={
        <>
          <button
            type="button"
            onClick={onCancel}
            className="rounded-md border border-[var(--border)] px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--surface-2)]"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={crop}
            disabled={busy || !image}
            className="rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-60"
          >
            {busy ? "Enviando..." : "Usar este enquadramento"}
          </button>
        </>
      }
    >
      <p className="mb-3 text-sm text-[var(--text-secondary)]">
        Arraste a imagem dentro da moldura e aproxime até caber. A moldura tem a proporção do
        espaço no papel: o que aparece aqui é o que sai impresso.
      </p>

      <div
        className="mx-auto overflow-hidden rounded-lg border-2 border-dashed border-[var(--primary)]"
        style={{
          width: frame.width,
          height: frame.height,
          maxWidth: "100%",
          backgroundImage:
            "linear-gradient(45deg, var(--surface-2) 25%, transparent 25%, transparent 75%, var(--surface-2) 75%), linear-gradient(45deg, var(--surface-2) 25%, transparent 25%, transparent 75%, var(--surface-2) 75%)",
          backgroundSize: "16px 16px",
          backgroundPosition: "0 0, 8px 8px",
        }}
      >
        <canvas
          ref={canvasRef}
          width={frame.width}
          height={frame.height}
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerCancel={onPointerUp}
          className="h-full w-full cursor-grab touch-none active:cursor-grabbing"
          aria-label="Área de enquadramento do logotipo"
        />
      </div>

      <label className="mt-4 block">
        <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
          Aproximar
        </span>
        <input
          type="range"
          min={1}
          max={4}
          step={0.01}
          value={zoom}
          onChange={(event) => setZoom(Number(event.target.value))}
          className="w-full accent-[var(--primary)]"
        />
      </label>
    </Modal>
  );
}
