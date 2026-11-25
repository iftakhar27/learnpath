"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT } from "@/lib/i18n";
import { Button, buttonClass } from "@/components/ui/button";
import { PageLoading, SkeletonRows } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { SubjectIcon, tone } from "../learn/subjects";
import { routes } from "../routes";
import { PageTitle, ProgressBar, useFormat } from "../ui";

type Deck = components["schemas"]["FlashcardDeckDto"];
type Card = components["schemas"]["FlashcardDto"];

export const flashcardsKey = ["student", "flashcards"] as const;

/** Every chapter with cards, grouped by NCERT book, with what's due and what's mastered. */
export function FlashcardDecks() {
  const t = useT();
  const q = useQuery({ queryKey: flashcardsKey, queryFn: () => unwrap(api.GET("/api/v1/flashcards")) });
  const books = new Map<string, Deck[]>();
  q.data?.decks.forEach((d) => books.set(`${d.subject}-${d.classLevel}`, [...(books.get(`${d.subject}-${d.classLevel}`) ?? []), d]));

  return (
    <>
      <PageTitle
        subtitle={t("flashcardsIntro")}
        actions={q.data && q.data.dueTotal > 0 ? <span className="rounded-full bg-primary-soft px-3 py-1 text-sm font-medium text-primary">{t("flashcardsDue", { count: q.data.dueTotal })}</span> : undefined}
      >
        {t("flashcardsTitle")}
      </PageTitle>
      {q.isPending ? (
        <SkeletonRows rows={4} />
      ) : q.isError ? (
        <StudentError error={q.error} onRetry={() => q.refetch()} />
      ) : q.data.decks.length === 0 ? (
        <p className="rounded-2xl border border-dashed border-border p-6 text-center text-fg-muted">{t("flashcardsEmpty")}</p>
      ) : (
        <div className="space-y-6">
          {[...books.values()].map((decks) => (
            <section key={`${decks[0].subject}-${decks[0].classLevel}`} aria-label={`${decks[0].subjectTitle} · ${t("classLevel", { level: decks[0].classLevel })}`}>
              <h2 className="mb-2 font-semibold">
                {decks[0].subjectTitle} · {t("classLevel", { level: decks[0].classLevel })}
              </h2>
              <ul className="grid gap-3 sm:grid-cols-2">
                {decks.map((d) => (
                  <li key={d.chapterId}>
                    <DeckLink deck={d} />
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      )}
    </>
  );
}

function DeckLink({ deck }: { deck: Deck }) {
  const t = useT();
  const tn = tone(deck.subject);
  return (
    <Link href={routes.flashcardDeck(deck.chapterId)} className="flex h-full items-start gap-3 rounded-2xl border border-border bg-surface p-4 hover:border-primary">
      <span className={`flex size-11 shrink-0 items-center justify-center rounded-xl ${tn.soft} ${tn.text}`}>
        <SubjectIcon subject={deck.subject} classLevel={deck.classLevel} size={24} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-xs text-fg-muted">{t("chapterNumber", { number: deck.number })}</span>
        <span className="block font-semibold">{deck.title}</span>
        <span className="mt-1 flex flex-wrap gap-x-2 text-sm text-fg-muted">
          <span>{t("flashcardsCount", { count: deck.cards })}</span>
          <span aria-hidden>·</span>
          <span className={deck.due > 0 ? "font-medium text-primary" : ""}>{t("flashcardsDue", { count: deck.due })}</span>
          {deck.mastered > 0 && (
            <>
              <span aria-hidden>·</span>
              <span>{t("flashcardsMastered", { count: deck.mastered })}</span>
            </>
          )}
        </span>
        <span className="mt-2 block">
          <ProgressBar value={(100 * deck.mastered) / Math.max(1, deck.cards)} label={t("flashcardsMastered", { count: deck.mastered })} />
        </span>
      </span>
    </Link>
  );
}

/**
 * One study session: the due cards (new ones included), or every card when asked. Flip with a click, Space or Enter;
 * answer with the buttons, or ← (still learning) and → (got it).
 */
export function FlashcardStudy() {
  const t = useT();
  const f = useFormat();
  const qc = useQueryClient();
  const { chapterId } = useParams<{ chapterId: string }>();
  const q = useQuery({
    queryKey: [...flashcardsKey, chapterId],
    queryFn: () => unwrap(api.GET("/api/v1/flashcards/{chapterId}", { params: { path: { chapterId } } })),
    // Loaded once per session: answering cards doesn't reshuffle the one in progress.
    staleTime: Infinity,
  });
  // A session the student started explicitly; until then, the cards due when the deck loaded.
  const [started, setStarted] = useState<Card[] | null | undefined>(undefined);
  const dueAtLoad = useMemo(() => q.data?.cards.filter((c) => c.isDue) ?? [], [q.data]);
  const session = started !== undefined ? started : dueAtLoad.length > 0 ? dueAtLoad : null;
  const [index, setIndex] = useState(0);
  const [flipped, setFlipped] = useState(false);
  const [known, setKnown] = useState(0);
  const review = useMutation({
    mutationFn: (v: { id: string; known: boolean }) => unwrap(api.POST("/api/v1/flashcards/cards/{cardId}/review", { params: { path: { cardId: v.id } }, body: { known: v.known } })),
    onSuccess: () => void qc.invalidateQueries({ queryKey: flashcardsKey, exact: true }),
  });

  const start = (cards: Card[] | null) => {
    setStarted(cards);
    setIndex(0);
    setFlipped(false);
    setKnown(0);
  };

  const card = session?.[index];
  const answer = useCallback(
    async (isKnown: boolean) => {
      if (!card || !flipped || review.isPending) return;
      await review.mutateAsync({ id: card.id, known: isKnown });
      if (isKnown) setKnown((k) => k + 1);
      setFlipped(false);
      setIndex((i) => i + 1);
    },
    [card, flipped, review],
  );
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (!card || e.target instanceof HTMLButtonElement || e.target instanceof HTMLInputElement) return;
      if (e.key === " " || e.key === "Enter") {
        e.preventDefault();
        setFlipped((x) => !x);
      } else if (e.key === "ArrowRight") void answer(true);
      else if (e.key === "ArrowLeft") void answer(false);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [card, answer]);

  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const { deck, nextDueAt } = q.data;
  const tn = tone(deck.subject);
  const finished = session !== null && index >= session.length;
  const again = finished ? session.length - known : 0;
  const restart = async () => {
    const fresh = await q.refetch();
    const due = fresh.data?.cards.filter((c) => c.isDue) ?? [];
    start(due.length > 0 ? due : null);
  };

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <Link href={routes.flashcards} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
        <Icon name="chevronLeft" size={16} />
        {t("allDecks")}
      </Link>
      <header className="flex items-center gap-3">
        <span className={`flex size-12 shrink-0 items-center justify-center rounded-xl ${tn.soft} ${tn.text}`}>
          <SubjectIcon subject={deck.subject} classLevel={deck.classLevel} size={26} />
        </span>
        <div className="min-w-0">
          <p className="text-sm text-fg-muted">
            {deck.subjectTitle} · {t("classLevel", { level: deck.classLevel })} · {t("chapterNumber", { number: deck.number })}
          </p>
          <h1 className="text-2xl font-semibold">{deck.title}</h1>
        </div>
      </header>

      {session === null ? (
        <section className="rounded-2xl border border-border bg-surface p-6 text-center">
          <p className="text-lg font-semibold">{t("allCaughtUp")}</p>
          {nextDueAt && <p className="mt-1 text-fg-muted">{t("nextCardsDue", { date: f.date(nextDueAt) })}</p>}
          <Button className="mt-4" onClick={() => start(q.data.cards)}>
            {t("reviseAll")}
          </Button>
        </section>
      ) : finished ? (
        <section className="rounded-2xl border border-border bg-surface p-6 text-center" aria-live="polite">
          <p className="text-lg font-semibold">{t("sessionDone")}</p>
          <p className="mt-1">{t("sessionKnew", { known, total: session.length })}</p>
          {again > 0 && <p className="text-fg-muted">{t("sessionAgainSoon", { count: again })}</p>}
          <div className="mt-4 flex flex-wrap justify-center gap-2">
            <Button onClick={() => void restart()}>{t("studyAgain")}</Button>
            <Link href={routes.flashcards} className={buttonClass("secondary")}>
              {t("allDecks")}
            </Link>
          </div>
        </section>
      ) : (
        card && (
          <section aria-label={t("flashcardsTitle")} className="space-y-4">
            <div className="flex items-center justify-between gap-3 text-sm text-fg-muted">
              <span>{t("cardProgress", { current: index + 1, total: session.length })}</span>
              <span>{t("flashcardsMastered", { count: deck.mastered })}</span>
            </div>
            <ProgressBar value={(100 * index) / session.length} label={t("cardProgress", { current: index + 1, total: session.length })} />
            <div className="perspective-distant">
              <button
                type="button"
                onClick={() => setFlipped((x) => !x)}
                aria-label={flipped ? `${t("cardBack")}: ${card.back}` : `${t("cardFront")}: ${card.front}. ${t("tapToFlip")}`}
                className={`relative grid min-h-64 w-full transition-transform duration-500 transform-3d motion-reduce:transition-none ${flipped ? "rotate-y-180" : ""}`}
              >
                <span className={`col-start-1 row-start-1 flex flex-col items-center justify-center gap-3 rounded-2xl border border-border bg-surface p-6 text-center shadow-sm backface-hidden`}>
                  <span className={`text-xs font-semibold uppercase tracking-wide ${tn.text}`}>{t("cardFront")}</span>
                  <span className="text-xl font-semibold">{card.front}</span>
                  <span className="text-sm text-fg-muted">{t("tapToFlip")}</span>
                </span>
                <span className={`col-start-1 row-start-1 flex rotate-y-180 flex-col items-center justify-center gap-3 rounded-2xl border border-border p-6 text-center shadow-sm backface-hidden ${tn.soft}`}>
                  <span className={`text-xs font-semibold uppercase tracking-wide ${tn.text}`}>{t("cardBack")}</span>
                  <span className="whitespace-pre-line text-lg">{card.back}</span>
                </span>
              </button>
            </div>
            <p className="sr-only" aria-live="polite">
              {flipped ? `${t("cardBack")}: ${card.back}` : ""}
            </p>
            {flipped ? (
              <div className="grid grid-cols-2 gap-3">
                <Button variant="secondary" onClick={() => void answer(false)} disabled={review.isPending}>
                  {t("stillLearning")}
                </Button>
                <Button onClick={() => void answer(true)} disabled={review.isPending}>
                  {t("gotIt")}
                </Button>
              </div>
            ) : (
              <Button variant="secondary" className="w-full" onClick={() => setFlipped(true)}>
                {t("showAnswer")}
              </Button>
            )}
            <StudentError error={review.error} />
          </section>
        )
      )}
    </div>
  );
}
