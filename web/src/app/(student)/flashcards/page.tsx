import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { FlashcardDecks } from "@/features/student/flashcards/flashcards";

export const metadata: Metadata = { title: "Flash cards" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <FlashcardDecks />
    </Suspense>
  );
}
