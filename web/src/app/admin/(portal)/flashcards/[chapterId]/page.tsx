import type { Metadata } from "next";
import { RoleGate } from "@/components/admin/shell";
import { FlashcardDeckEditor } from "@/features/admin/flashcards";

export const metadata: Metadata = { title: "Flash cards" };

export default function Page() {
  return (
    <RoleGate permission="editContent">
      <FlashcardDeckEditor />
    </RoleGate>
  );
}
