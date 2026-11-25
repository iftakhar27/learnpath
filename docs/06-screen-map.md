# Screen Inventory & Navigation Map

```mermaid
flowchart TD
  Splash --> Intro[Intro slides · first launch only] --> Auth{Logged in?}
  Auth -- no --> Login[Login: OTP / email] --> Reg[Register] --> Setup[Profile setup:<br/>class, board, exam, year, language, study hours]
  Auth -- explore --> GuestMode[Guest: subjects, courses, chapters<br/>practice/tests/profile ask to sign up]
  Auth -- yes --> Home
  Setup --> Home

  subgraph Tabs[Bottom navigation]
    Home
    Learn
    Practice
    Tests
    Profile
  end

  Home --> Plan[Study planner] & Cont[Continue lesson] & Res[Recent result] & Weak[Weak topics] & Notif[Notifications] & Search
  Learn --> Catalog[Course catalog] --> CDetail[Course details] --> Checkout --> PayStatus[Payment status]
  CDetail --> Subj[Subjects → chapters] --> Topic[Topic page]
  Topic --> Video[Video player] & Notes[Notes viewer] & PracCfg
  Practice --> PracCfg[Practice config] --> QPlayer[Question player] --> PracSum[Practice summary]
  Practice --> Revision[Revision centre] --> Bookmarks & Incorrect[Incorrect questions]
  Practice --> Flash[Flash cards: decks → study]
  Learn --> Materials[Free study material: filters → download]
  Tests --> Gen[Custom test generator] --> Instr
  Tests --> TList[Test list] --> Instr[Instructions] --> Exam[Exam interface<br/>palette, timer, review] --> Confirm[Submit confirmation] --> Result[Results & analytics] --> Review[Question review]
  Tests --> PYQ[Previous-year papers · P1]
  Profile --> Settings & Subs[Subscriptions] & Txn[Transactions] & Help[Help & support] --> Ticket[Ticket detail]
  Tabs -. ☰ .-> Menu[Side menu: study pages, help, night mode, language, sign out]
```

Every tab screen (and the website header) has a ☰ side menu with the same sections in both: the student, Study, Help,
Settings (night mode, language, preferences) and Sign out. Night mode is Auto / Light / Dark: a switch in the menu, all
three in Profile → Appearance.

## Admin portal (web)

```
/admin/login
/admin                 dashboard (counts, attempts, payments, reported issues)
/admin/catalog         exams → courses → subjects → chapters → topics → resources (tree editor)
/admin/questions       question bank list, filters, editor, CSV import (P1)
/admin/tests           test builder (pick questions, marks, negative marks, duration, shuffle)
/admin/students        search, status, enrollments
/admin/orders          orders, payments, refunds
/admin/support         tickets, question reports
/admin/audit           audit log viewer
```

## Design system (original — no Embibe assets)

| Token | Value |
|---|---|
| Primary | `#4F46E5` (indigo 600) |
| Accent | `#F59E0B` (amber 500, streaks/highlights) |
| Success / Error | `#16A34A` / `#DC2626` |
| Surface / Background | `#FFFFFF` / `#F7F7FB`; dark (night mode): `#1A1B26` / `#0F1018` |
| Radius | 12 px cards, 8 px inputs, 999 px chips |
| Spacing scale | 4, 8, 12, 16, 24, 32 |
| Type | Noto Sans + Noto Sans Devanagari (covers Hindi), 14/16/20/24/32 |
| Touch target | ≥ 48 dp |

Reusable components: `AppButton`, `ProgressRing`, `TopicTile`, `QuestionCard`, `OptionTile`, `QuestionPalette`,
`CountdownTimer`, `StatTile`, `EmptyState`, `ErrorState`, `SkeletonList`, `PaywallSheet`.
Every list screen implements loading (skeleton), empty, error (retry) and offline states.
