import type { Metadata } from "next";
import { RoleGate } from "@/components/admin/shell";
import { FlashcardDecksAdmin } from "@/features/admin/flashcards";

export const metadata: Metadata = { title: "Flash cards" };

export default function Page() {
  return (
    <RoleGate permission="editContent">
      <FlashcardDecksAdmin />
    </RoleGate>
  );
}
