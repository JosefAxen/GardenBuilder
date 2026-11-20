# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Customers:** Swedish homeowners who want to redo their garden. They come with photos of the garden as it is and
  an idea of a style and a budget, but not the knowledge to plan plants, spacing and paving. They design on a laptop
  at home; on a phone they upload photos and look, but do not edit in 3D.
- **Sellers:** staff at the garden company. They open incoming quote requests, adjust the customer's design in the
  same 3D editor, mark measurements as verified after a site visit and send a binding quote.
- **Administrators:** staff who keep articles, prices, labour norms, stock, AI quotas and retention times.

## Product Purpose

Drömträdgård is a sales tool for one Swedish company that plans, sells and builds gardens. The customer uploads photos,
gets the garden rebuilt as a stylised 3D scene, gets AI design proposals, shapes their own design from the company's
stock and sees an approximate price. The flow ends in a quote request; a seller turns it into a binding quote.
Success: a customer goes from uploaded photo to sent quote request on their own.

## Positioning

The customer designs in the real company's stock, in their own garden's shape, and every price is a range that
honestly shows how sure it is. Nothing in a design is something the company cannot deliver.

## Operating Context

- Customer flow in six steps: start (photos, address), current state (AI reads photos, customer corrects), proposals
  (style, budget, care level, plant zone), own design (swap, move, add from the catalogue), dream image, quote request.
  Every AI step has a manual fallback so no one gets stuck.
- Sellers work a case list with statuses Ny, Under arbete, Platsbesök bokat, Offert skickad, Vunnen, Förlorad, and send
  the quote as an editable line list and PDF.
- The 3D scene is stylised (Quaternius models and code-generated shapes), seen from above or in perspective.

## Capabilities and Constraints

- Everything the user sees is Swedish. Prices in SEK including VAT, shown as a span, calculated only on the server.
- React and TypeScript with React Three Fiber; the 3D editor and its panels share state in a zustand store.
- 3D editing on desktop only; mobile handles upload and viewing.
- Built by one developer; ready for real customers by 13 December 2026.
- Undecided: e-mail service, Azure details, the dream-image provider.

## Brand Commitments

None. The company has no logo or colours the app must follow (Josef, 2026-11-19). The name Drömträdgård is the only
brand asset.

## Evidence on Hand

- Real stock articles come from the company's Excel list; seed data in the repository is synthetic.
- No testimonials, customer counts or reviews exist; none may be invented.

## Product Principles

- A price never looks more exact than its measurements allow.
- The customer is never stuck: every automated step has a manual way through.
- Only what the company can deliver appears in a design.
- The customer's own garden, not a template, is the subject.

## Accessibility & Inclusion

Customers are ordinary homeowners of all ages, not designers; plain Swedish, readable sizes and clear states matter
more than density.
