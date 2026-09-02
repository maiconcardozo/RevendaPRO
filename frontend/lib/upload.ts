import { apiUpload, type Result } from "./api";

/** What a batch upload left behind: how many went up, and what failed on each one. */
export type UploadOutcome = {
  sent: number;
  failures: { file: string; error: string }[];
};

/**
 * Uploads several files, **one at a time**.
 *
 * In series on purpose. Each image becomes WebP in three sizes on the server, and firing
 * twenty requests at once lets one person occupy the whole API. In series the count moves on
 * screen and the server keeps serving whoever else is working.
 *
 * A refused file never takes the batch down: somebody who sent twenty photos and had one
 * refused keeps the nineteen, and reads why the odd one stayed out.
 */
export async function apiUploadMany(
  path: string,
  files: File[],
  fields: Record<string, string>,
  fallback: string,
  onProgress?: (done: number, total: number) => void,
  maxSizeInBytes?: number,
): Promise<UploadOutcome> {
  const failures: UploadOutcome["failures"] = [];
  let sent = 0;

  for (const [index, file] of files.entries()) {
    onProgress?.(index, files.length);

    // The size is known before the first byte leaves. Sending anyway would spend the whole
    // upload to hear a 413 at the end — and on a very large file the server refuses without
    // reading the body, so the connection drops before the answer arrives. The limit comes
    // from the server, which stays the one that decides.
    if (maxSizeInBytes && file.size > maxSizeInBytes) {
      const megabytes = maxSizeInBytes / (1024 * 1024);

      failures.push({
        file: file.name,
        error: `${file.name} passa do limite. Envie um arquivo de até ${megabytes
          .toFixed(1)
          .replace(/\.0$/, "")
          .replace(".", ",")} MB.`,
      });

      continue;
    }

    const result: Result<unknown> = await apiUpload(path, file, fields, fallback);

    if (result.ok) {
      sent++;
    } else {
      failures.push({ file: file.name, error: result.error });
    }
  }

  onProgress?.(files.length, files.length);

  return { sent, failures };
}
