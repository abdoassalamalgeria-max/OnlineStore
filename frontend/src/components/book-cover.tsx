import Image from "next/image";

// Muted palette for generated covers; picked deterministically from the title.
const palette = ["#0f6b4f", "#7c2d12", "#1e3a8a", "#6b21a8", "#854d0e", "#115e59", "#9f1239"];

function colorFor(text: string) {
  let hash = 0;
  for (const char of text) {
    hash = (hash * 31 + char.charCodeAt(0)) | 0;
  }
  return palette[Math.abs(hash) % palette.length];
}

type Props = {
  title: string;
  url: string | null | undefined;
  /** Book language, so the placeholder title is rendered with the right script direction. */
  language: string;
  /** Pass true for above-the-fold covers (the book page). */
  priority?: boolean;
  sizes?: string;
};

/** Cover image, or a generated placeholder showing the title when the book has none yet. */
export function BookCover({
  title,
  url,
  language,
  priority,
  sizes = "(min-width: 768px) 25vw, 50vw",
}: Props) {
  if (url) {
    return (
      <Image
        src={url}
        alt={title}
        fill
        sizes={sizes}
        priority={priority}
        // Covers are already resized to 600 px WebP by the API; re-optimizing adds nothing, and
        // Next 16 refuses to optimize images from local hosts (the API in development).
        unoptimized
        className="rounded-md object-cover"
      />
    );
  }

  return (
    <div
      role="img"
      aria-label={title}
      className="flex size-full items-center justify-center rounded-md p-4 text-center text-white"
      style={{ backgroundColor: colorFor(title) }}
    >
      <span lang={language} dir="auto" className="line-clamp-4 text-lg leading-snug font-bold">
        {title}
      </span>
    </div>
  );
}
