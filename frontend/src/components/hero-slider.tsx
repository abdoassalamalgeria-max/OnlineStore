"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

export type Slide = {
  image: string;
  title: string;
  text: string;
  cta: { href: string; label: string };
};

type Labels = {
  carousel: string;
  previous: string;
  next: string;
  goTo: string; // "{n}" is replaced by the slide number
  pause: string;
  play: string;
};

const INTERVAL_MS = 6000;

/**
 * Full-width home slider (edge to edge, breaking out of the page container). Slides cross-fade, so it
 * works the same in RTL and LTR. Auto-advances, but pauses on hover/focus, when the tab is hidden, when
 * the visitor presses pause, and never starts for people who prefer reduced motion.
 */
export function HeroSlider({
  slides,
  labels,
  dir,
}: {
  slides: Slide[];
  labels: Labels;
  dir: "rtl" | "ltr";
}) {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);
  const [hovered, setHovered] = useState(false);
  const [reducedMotion, setReducedMotion] = useState(false);
  const root = useRef<HTMLElement>(null);
  const count = slides.length;

  const go = useCallback((i: number) => setIndex(((i % count) + count) % count), [count]);

  useEffect(() => {
    const query = window.matchMedia("(prefers-reduced-motion: reduce)");
    const update = () => setReducedMotion(query.matches);
    update();
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);

  useEffect(() => {
    if (paused || hovered || reducedMotion || count < 2) return;
    const id = window.setInterval(() => {
      if (document.visibilityState === "visible") setIndex((i) => (i + 1) % count);
    }, INTERVAL_MS);
    return () => window.clearInterval(id);
  }, [paused, hovered, reducedMotion, count]);

  // In RTL the "previous" control sits on the right and points right, mirroring LTR.
  const onKeyDown = (e: React.KeyboardEvent) => {
    const forward = dir === "rtl" ? "ArrowLeft" : "ArrowRight";
    const back = dir === "rtl" ? "ArrowRight" : "ArrowLeft";
    if (e.key === forward) go(index + 1);
    else if (e.key === back) go(index - 1);
  };

  return (
    <section
      ref={root}
      aria-roledescription="carousel"
      aria-label={labels.carousel}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onFocusCapture={() => setHovered(true)}
      onBlurCapture={(e) => {
        if (!root.current?.contains(e.relatedTarget as Node)) setHovered(false);
      }}
      onKeyDown={onKeyDown}
      // Full bleed: 100vw wide, pulled out of the centered page container on both sides.
      className="relative ms-[calc(50%-50vw)] w-screen overflow-hidden bg-[#1e4226]"
    >
      <div className="relative h-[360px] sm:h-[420px] lg:h-[480px]">
        {slides.map((slide, i) => (
          <div
            key={slide.image}
            role="group"
            aria-roledescription="slide"
            aria-label={`${i + 1} / ${count}`}
            aria-hidden={i !== index}
            inert={i !== index}
            className={`absolute inset-0 transition-opacity duration-700 ease-out ${
              i === index ? "opacity-100" : "opacity-0"
            }`}
          >
            {/* Decorative illustration: the slide's meaning is carried by the text below. */}
            {/* eslint-disable-next-line @next/next/no-img-element -- local SVG art, nothing to optimize */}
            <img
              src={slide.image}
              alt=""
              className="absolute inset-0 h-full w-full object-cover"
              fetchPriority={i === 0 ? "high" : "low"}
              loading={i === 0 ? "eager" : "lazy"}
            />
            {/* Readability scrim on the text side (start side in both directions). */}
            <div className="absolute inset-0 bg-gradient-to-r from-black/65 via-black/35 to-transparent rtl:bg-gradient-to-l" />
            <div className="relative mx-auto flex h-full max-w-6xl items-center px-6 sm:px-10">
              <div className="max-w-xl text-white">
                <h2 className="text-3xl leading-snug font-bold sm:text-4xl lg:text-5xl">
                  {slide.title}
                </h2>
                <p className="mt-4 text-lg text-white/90 sm:text-xl">{slide.text}</p>
                <Link
                  href={slide.cta.href}
                  className="text-brand mt-7 inline-block rounded-md bg-white px-6 py-3 font-bold shadow-sm hover:bg-[#f6f1e1]"
                >
                  {slide.cta.label}
                </Link>
              </div>
            </div>
          </div>
        ))}
      </div>

      {count > 1 && (
        <>
          <button
            type="button"
            onClick={() => go(index - 1)}
            aria-label={labels.previous}
            className="absolute start-3 top-1/2 hidden h-11 w-11 -translate-y-1/2 items-center justify-center rounded-full bg-white/85 text-2xl text-[#1e4226] shadow hover:bg-white sm:flex"
          >
            <span aria-hidden="true" className="rtl:-scale-x-100">
              ‹
            </span>
          </button>
          <button
            type="button"
            onClick={() => go(index + 1)}
            aria-label={labels.next}
            className="absolute end-3 top-1/2 hidden h-11 w-11 -translate-y-1/2 items-center justify-center rounded-full bg-white/85 text-2xl text-[#1e4226] shadow hover:bg-white sm:flex"
          >
            <span aria-hidden="true" className="rtl:-scale-x-100">
              ›
            </span>
          </button>

          <div className="absolute inset-x-0 bottom-4 flex items-center justify-center gap-3">
            {slides.map((slide, i) => (
              <button
                key={slide.image}
                type="button"
                onClick={() => go(i)}
                aria-label={labels.goTo.replace("{n}", String(i + 1))}
                aria-current={i === index}
                className={`h-2.5 rounded-full transition-all ${
                  i === index ? "bg-gold w-8" : "w-2.5 bg-white/70 hover:bg-white"
                }`}
              />
            ))}
            {!reducedMotion && (
              <button
                type="button"
                onClick={() => setPaused((p) => !p)}
                aria-label={paused ? labels.play : labels.pause}
                className="ms-2 flex h-7 w-7 items-center justify-center rounded-full bg-white/80 text-xs text-[#1e4226] hover:bg-white"
              >
                <span aria-hidden="true">{paused ? "▶" : "❚❚"}</span>
              </button>
            )}
          </div>
        </>
      )}
    </section>
  );
}
