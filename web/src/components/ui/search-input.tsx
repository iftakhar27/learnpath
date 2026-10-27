"use client";

import { useState } from "react";
import { Field, Input } from "./field";

/**
 * Search box that submits on Enter or blur. Render it with `key={value}` so it resets when the
 * committed value changes elsewhere (e.g. back/forward navigation changes the URL).
 */
export function SearchInput({ id, label, hint, placeholder, value, onSearch }: { id: string; label: string; hint?: string; placeholder?: string; value: string; onSearch: (v: string) => void }) {
  const [text, setText] = useState(value);
  const commit = () => text.trim() !== value && onSearch(text.trim());
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        commit();
      }}
    >
      <Field label={label} htmlFor={id} hint={hint}>
        <Input id={id} type="search" placeholder={placeholder} value={text} onChange={(e) => setText(e.target.value)} onBlur={commit} />
      </Field>
    </form>
  );
}
