"use client";

import { useCallback, useEffect, useState, type ReactNode } from "react";
import { ArrowLeft, ArrowRight, ImagePlus, Star, Trash2, X } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { apiGet, apiSend } from "@/lib/api";
import { apiUploadMany } from "@/lib/upload";
import { VEHICLE_PHOTO_KIND, VEHICLE_PHOTO_KIND_LABEL, type VehiclePhoto } from "@/lib/types";
import { Empty, PageError } from "./VehicleUi";

/**
 * The gallery of the vehicle.
 *
 * The order is curated by hand because the first photo is the one that goes into the
 * advertisement, and the sequence tells a story: the finished car first, the damage after,
 * for the buyer who asks.
 *
 * Deleting a photo **erases the bytes**. A gallery that keeps every discarded frame grows
 * without limit, and a photo taken out of the advertisement has no second life — unlike a
 * document, which stays in the store forever.
 */
export function PhotosPanel({
  vehicleCode,
  maxUploadSize,
  onChanged,
}: {
  vehicleCode: string;
  /** Largest accepted file. Refusing here saves the whole upload. */
  maxUploadSize: number;
  /** The cover and the count show in the listing, so the whole sheet reloads. */
  onChanged: () => void;
}) {
  const [photos, setPhotos] = useState<VehiclePhoto[] | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [uploading, setUploading] = useState<{ done: number; total: number } | null>(null);
  const [kind, setKind] = useState(String(VEHICLE_PHOTO_KIND.finished));
  const [toDelete, setToDelete] = useState<VehiclePhoto | null>(null);
  const [zoomed, setZoomed] = useState<VehiclePhoto | null>(null);

  const load = useCallback(async () => {
    const result = await apiGet<VehiclePhoto[]>(
      `vehicles/${vehicleCode}/photos`,
      "Falha ao carregar as fotos.",
    );

    if (result.ok) {
      setPhotos(result.data);
      setError("");
    } else {
      setPhotos([]);
      setError(result.error);
    }
  }, [vehicleCode]);

  useEffect(() => {
    load();
  }, [load]);

  /**
   * Uploads the chosen photos, one at a time.
   *
   * In series on purpose: each image becomes WebP in three sizes on the server, and sending
   * twenty at once would leave the API processing twenty in parallel for one person. In
   * series the count moves and the server breathes.
   */
  async function upload(files: FileList | null) {
    if (!files || files.length === 0) return;

    setError("");

    const outcome = await apiUploadMany(
      `vehicles/${vehicleCode}/photos`,
      [...files],
      { kind },
      "Falha ao enviar a foto.",
      (done, total) => setUploading({ done, total }),
      maxUploadSize,
    );

    setUploading(null);

    if (outcome.failures.length > 0) {
      setError(
        outcome.failures.length === files.length
          ? outcome.failures[0].error
          : `${outcome.sent} de ${files.length} fotos subiram. ${outcome.failures[0].error}`,
      );
    }

    await load();
    onChanged();
  }

  /** Sends the whole order, and never one move at a time. See ReorderPhotosRequest. */
  async function reorder(from: number, to: number) {
    if (!photos || to < 0 || to >= photos.length) return;

    const next = [...photos];
    const [moved] = next.splice(from, 1);
    next.splice(to, 0, moved);

    // Moves on screen first: the arrow answers at once, and the server confirms after.
    setPhotos(next);
    setBusy(true);

    const result = await apiSend(
      "PATCH",
      `vehicles/${vehicleCode}/photos/order`,
      "Falha ao reordenar as fotos.",
      { codes: next.map((photo) => photo.code) },
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      await load();
    }
  }

  async function setCover(photo: VehiclePhoto) {
    setBusy(true);

    const result = await apiSend(
      "PUT",
      `vehicles/${vehicleCode}/cover`,
      "Falha ao definir a capa.",
      { photoCode: photo.code },
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    await load();
    onChanged();
  }

  async function reclassify(photo: VehiclePhoto, newKind: string) {
    const result = await apiSend(
      "PATCH",
      `vehicles/${vehicleCode}/photos/${photo.code}/kind`,
      "Falha ao classificar a foto.",
      { kind: Number(newKind) },
    );

    if (!result.ok) {
      setError(result.error);
      return;
    }

    await load();
  }

  async function remove(photo: VehiclePhoto) {
    setBusy(true);

    const result = await apiSend(
      "DELETE",
      `vehicles/${vehicleCode}/photos/${photo.code}`,
      "Falha ao excluir a foto.",
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    setToDelete(null);
    await load();
    onChanged();
  }

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm text-[var(--text-secondary)]">
            {photos === null
              ? "Carregando…"
              : `${photos.length} ${photos.length === 1 ? "foto" : "fotos"}`}
          </p>
          <p className="mt-0.5 text-xs text-[var(--text-muted)]">
            Vira WebP em três tamanhos, e o servidor descarta o EXIF — inclusive a coordenada
            de GPS que o celular grava.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <select
            value={kind}
            onChange={(event) => setKind(event.target.value)}
            aria-label="Para que serve a foto"
            className="rounded-md border border-[var(--border)] bg-[var(--canvas)] px-3 py-2 text-sm"
          >
            {Object.entries(VEHICLE_PHOTO_KIND_LABEL).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>

          <label className="inline-flex cursor-pointer items-center gap-2 rounded-md bg-[var(--primary)] px-3.5 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]">
            <ImagePlus size={16} />
            {uploading ? `Enviando ${uploading.done}/${uploading.total}…` : "Enviar fotos"}
            <input
              type="file"
              multiple
              accept="image/jpeg,image/png,image/webp"
              className="sr-only"
              disabled={uploading !== null}
              onChange={(event) => {
                upload(event.target.files);

                // Cleared so choosing the same file again fires the event once more.
                event.target.value = "";
              }}
            />
          </label>
        </div>
      </div>

      <PageError message={error} />

      {photos !== null && photos.length === 0 ? (
        <Empty title="Nenhuma foto ainda. A primeira que subir vira a capa." />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {(photos ?? []).map((photo, index) => (
            <figure
              key={photo.code}
              className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]"
            >
              <div className="relative aspect-[4/3] bg-[var(--surface-2)]">
                <button
                  type="button"
                  onClick={() => setZoomed(photo)}
                  className="h-full w-full"
                  aria-label={`Ampliar foto ${index + 1}`}
                >
                  {/* Signed address, short lived: optimizing and caching would make no sense. */}
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={photo.cardUrl}
                    alt=""
                    loading="lazy"
                    className="h-full w-full object-cover"
                  />
                </button>

                {photo.isCover && (
                  <span className="absolute left-2 top-2 inline-flex items-center gap-1 rounded-full bg-[var(--primary)] px-2 py-1 text-[10px] font-bold uppercase tracking-wide text-white">
                    <Star size={11} />
                    Capa
                  </span>
                )}
              </div>

              <figcaption className="flex items-center justify-between gap-2 p-2.5">
                <select
                  value={String(photo.kind)}
                  onChange={(event) => reclassify(photo, event.target.value)}
                  aria-label={`Classificação da foto ${index + 1}`}
                  className="min-w-0 flex-1 rounded-md border border-[var(--border)] bg-[var(--canvas)] px-2 py-1 text-xs"
                >
                  {Object.entries(VEHICLE_PHOTO_KIND_LABEL).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>

                <div className="flex shrink-0 items-center gap-0.5">
                  <IconButton
                    label={`Mover a foto ${index + 1} para trás`}
                    disabled={busy || index === 0}
                    onClick={() => reorder(index, index - 1)}
                  >
                    <ArrowLeft size={14} />
                  </IconButton>

                  <IconButton
                    label={`Mover a foto ${index + 1} para a frente`}
                    disabled={busy || index === (photos?.length ?? 0) - 1}
                    onClick={() => reorder(index, index + 1)}
                  >
                    <ArrowRight size={14} />
                  </IconButton>

                  <IconButton
                    label={`Usar a foto ${index + 1} como capa`}
                    disabled={busy || photo.isCover}
                    onClick={() => setCover(photo)}
                    tone="var(--primary)"
                  >
                    <Star size={14} />
                  </IconButton>

                  <IconButton
                    label={`Excluir a foto ${index + 1}`}
                    disabled={busy}
                    onClick={() => setToDelete(photo)}
                    tone="var(--critical)"
                  >
                    <Trash2 size={14} />
                  </IconButton>
                </div>
              </figcaption>
            </figure>
          ))}
        </div>
      )}

      {zoomed && (
        <div
          className="panel-scrim grid place-items-center p-4"
          onMouseDown={() => setZoomed(null)}
          role="presentation"
        >
          <button
            type="button"
            onClick={() => setZoomed(null)}
            aria-label="Fechar"
            className="absolute right-5 top-5 grid h-9 w-9 place-items-center rounded-md bg-[rgba(11,30,63,.6)] text-white"
          >
            <X size={18} />
          </button>

          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={zoomed.fullUrl}
            alt=""
            onMouseDown={(event) => event.stopPropagation()}
            className="max-h-[88vh] max-w-full rounded-lg object-contain shadow-[var(--shadow-lg)]"
          />
        </div>
      )}

      {toDelete && (
        <Confirmation
          title="Excluir foto"
          message="A foto sai da galeria e o arquivo é apagado do armazenamento. Isto é definitivo."
          confirmLabel="Excluir"
          danger
          busy={busy}
          onConfirm={() => remove(toDelete)}
          onCancel={() => setToDelete(null)}
        />
      )}
    </div>
  );
}

function IconButton({
  label,
  onClick,
  disabled,
  tone,
  children,
}: {
  label: string;
  onClick: () => void;
  disabled?: boolean;
  tone?: string;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      title={label}
      className="grid h-7 w-7 place-items-center rounded-md text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent"
      onMouseEnter={(event) => {
        if (tone && !disabled) event.currentTarget.style.color = tone;
      }}
      onMouseLeave={(event) => {
        event.currentTarget.style.color = "";
      }}
    >
      {children}
    </button>
  );
}
