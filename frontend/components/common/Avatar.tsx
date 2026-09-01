"use client";

import { useState } from "react";

/**
 * With a photo, shows the photo. Without one, the initial of the name.
 * If the image fails to load it falls back to the initial instead of a broken icon.
 */
export function Avatar({
  name,
  code,
  hasPhoto,
  size = 36,
  version,
  className = "",
}: {
  name: string;
  code: string;
  hasPhoto: boolean;
  size?: number;
  version?: number;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);

  const initial = name.trim().charAt(0).toUpperCase() || "?";
  const showPhoto = hasPhoto && !failed;

  return (
    <span
      className={`grid shrink-0 place-items-center overflow-hidden rounded-full bg-[var(--primary)] font-bold text-white ${className}`}
      style={{ height: size, width: size, fontSize: Math.round(size * 0.42) }}
      aria-hidden
    >
      {showPhoto ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={`/api/backend/users/${code}/photo${version ? `?v=${version}` : ""}`}
          alt=""
          onError={() => setFailed(true)}
          className="h-full w-full object-cover"
        />
      ) : (
        initial
      )}
    </span>
  );
}
