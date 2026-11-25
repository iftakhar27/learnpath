/** Where the night-mode choice is kept (see theme.ts). Separate from theme.ts so the server layout can import it. */
export const THEME_KEY = "lp_theme";

/** Runs in <head> before anything renders, so a dark choice never flashes white. Keep it tiny and dependency-free. */
export const themeBootScript = `try{var t=localStorage.getItem("${THEME_KEY}");if(t==="light"||t==="dark")document.documentElement.dataset.theme=t}catch(e){}`;
