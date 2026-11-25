"use client";

import { createContext, useContext } from "react";

/**
 * Browsing without an account (no session cookie): the catalogue, course pages and the NCERT subject pages work;
 * anything that needs an account sends the visitor to sign in and back.
 */
const GuestContext = createContext(false);
export const GuestProvider = GuestContext.Provider;
export const useIsGuest = () => useContext(GuestContext);

/** For code outside React (the 401 handler): a guest is asked to sign in, not told their session ended. */
let guestNow = false;
export const isGuestNow = () => guestNow;
export function markGuest(on: boolean) {
  guestNow = on;
}
