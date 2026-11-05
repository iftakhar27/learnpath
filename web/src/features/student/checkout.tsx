"use client";

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";
import { useErrorText } from "./errors";
import { useFormat } from "./ui";

type Course = components["schemas"]["CourseSummaryDto"];
type Checkout = components["schemas"]["CheckoutDto"];

/** Public key of the API's simulated gateway (FakePaymentGateway) on test servers. */
const SIMULATED_KEY = "rzp_test_fake";
const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

type RazorpayResponse = { razorpay_order_id: string; razorpay_payment_id: string; razorpay_signature: string };
type RazorpayInstance = { open: () => void; on: (event: string, cb: (e: unknown) => void) => void };
type RazorpayCtor = new (options: Record<string, unknown>) => RazorpayInstance;

let scriptPromise: Promise<RazorpayCtor> | null = null;
function loadRazorpay(): Promise<RazorpayCtor> {
  const w = window as unknown as { Razorpay?: RazorpayCtor };
  if (w.Razorpay) return Promise.resolve(w.Razorpay);
  scriptPromise ??= new Promise<RazorpayCtor>((resolve, reject) => {
    const s = document.createElement("script");
    s.src = "https://checkout.razorpay.com/v1/checkout.js";
    s.async = true;
    s.onload = () => (w.Razorpay ? resolve(w.Razorpay) : reject(new Error("Razorpay unavailable")));
    s.onerror = () => {
      scriptPromise = null;
      reject(new Error("Razorpay unavailable"));
    };
    document.head.appendChild(s);
  });
  return scriptPromise;
}

/**
 * Buys a course: the API creates the order, Razorpay's checkout takes the payment, and access is granted only after
 * the API verifies the payment signature (a webhook also confirms it if the browser closes early).
 */
export function BuyButton({ course, onPurchased }: { course: Course; onPurchased?: () => void }) {
  const t = useT();
  const f = useFormat();
  const toast = useToast();
  const errorText = useErrorText();
  const qc = useQueryClient();
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ tone: "info" | "error"; text: string } | null>(null);

  const finish = async (r: RazorpayResponse) => {
    setNotice({ tone: "info", text: t("paymentVerifying") });
    try {
      await unwrap(api.POST("/api/v1/payments/verify", { body: { gatewayOrderId: r.razorpay_order_id, gatewayPaymentId: r.razorpay_payment_id, signature: r.razorpay_signature } }));
      setNotice(null);
      toast(t("paymentSuccess"));
      await qc.invalidateQueries();
      onPurchased?.();
    } catch (e) {
      setNotice({ tone: "error", text: errorText(e) });
    } finally {
      setBusy(false);
    }
  };

  const buy = async () => {
    setBusy(true);
    setNotice(null);
    let checkout: Checkout;
    try {
      checkout = await unwrap(api.POST("/api/v1/payments/orders", { body: { courseId: course.id } }));
    } catch (e) {
      setBusy(false);
      return setNotice({ tone: "error", text: errorText(e) });
    }
    if (checkout.keyId === SIMULATED_KEY) {
      setBusy(false);
      return setNotice({ tone: "info", text: t("paymentsSimulated") });
    }
    try {
      const Razorpay = await loadRazorpay();
      const rzp = new Razorpay({
        key: checkout.keyId,
        order_id: checkout.gatewayOrderId,
        amount: checkout.amountInPaise,
        currency: checkout.currency,
        name: brand,
        description: checkout.courseTitle,
        prefill: { email: checkout.prefillEmail ?? undefined, contact: checkout.prefillPhone ?? undefined },
        theme: { color: getComputedStyle(document.documentElement).getPropertyValue("--primary").trim() || "#4f46e5" },
        handler: (r: RazorpayResponse) => void finish(r),
        modal: {
          ondismiss: () => {
            setBusy(false);
            setNotice({ tone: "info", text: t("paymentCancelled") });
          },
        },
      });
      rzp.on("payment.failed", () => setNotice({ tone: "error", text: t("paymentFailed") }));
      rzp.open();
    } catch {
      setBusy(false);
      setNotice({ tone: "error", text: t("errorNetwork") });
    }
  };

  return (
    <div className="space-y-2">
      <Button onClick={buy} loading={busy} className="w-full sm:w-auto">
        {t("buyFor", { price: f.rupees(course.priceInPaise) })}
      </Button>
      {notice && (
        <p role={notice.tone === "error" ? "alert" : "status"} className={`rounded-lg px-3 py-2 text-sm ${notice.tone === "error" ? "bg-danger-soft text-danger" : "bg-primary-soft text-fg"}`}>
          {notice.text}
        </p>
      )}
    </div>
  );
}
