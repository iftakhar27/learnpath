import type { SVGProps } from "react";

/** Outline icons (24px grid, currentColor). Decorative by default: pass aria-label and role="img" when meaningful. */
const paths = {
  home: "M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6h-6v6H4a1 1 0 0 1-1-1z",
  book: "M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2zm0 16a2 2 0 0 1 2-2h13v2H6",
  pencil: "M4 20h4L19 9l-4-4L4 16zm9-13 4 4",
  clipboard: "M9 4h6v3H9zM7 5H5v16h14V5h-2M9 12h6M9 16h4",
  user: "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm-8 9a8 8 0 0 1 16 0",
  bell: "M6 16V11a6 6 0 1 1 12 0v5l2 2H4zm4 4h4",
  play: "M7 4v16l13-8z",
  doc: "M6 3h9l4 4v14H6zm9 0v4h4M9 12h7M9 16h7",
  check: "M5 12.5 10 17 19 7",
  flame: "M12 3c1 4 5 5 5 10a5 5 0 0 1-10 0c0-2 1-3.5 2-4.5.5 2 1.5 3 3 3-1-3 0-6 0-8.5z",
  clock: "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zm0-13v5l3 2",
  target: "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zm0-4a5 5 0 1 0 0-10 5 5 0 0 0 0 10zm0-4a1 1 0 1 0 0-2 1 1 0 0 0 0 2z",
  chevronRight: "m9 5 7 7-7 7",
  chevronLeft: "m15 5-7 7 7 7",
  lock: "M6 11h12v10H6zm2 0V7a4 4 0 0 1 8 0v4",
  bookmark: "M6 3h12v18l-6-4-6 4z",
  globe: "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM3 12h18M12 3c2.5 2.5 3.5 5.5 3.5 9s-1 6.5-3.5 9c-2.5-2.5-3.5-5.5-3.5-9s1-6.5 3.5-9z",
  flag: "M5 21V4m0 0h11l-2 4 2 4H5",
  x: "M6 6l12 12M18 6 6 18",
} as const;

export type IconName = keyof typeof paths;

export function Icon({ name, size = 20, ...rest }: { name: IconName; size?: number } & SVGProps<SVGSVGElement>) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden={rest["aria-label"] ? undefined : true} {...rest}>
      <path d={paths[name]} />
    </svg>
  );
}
