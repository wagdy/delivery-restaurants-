// The customer-facing name and phone rules, in one place.
//
// These were previously copy-pasted into every form that needed them - four copies in
// this app and five more in the backend's DTOs - and the copies drifted. The order and
// staff DTOs kept a Latin-only name rule long after registration started accepting
// Arabic, so a customer who registered as "محمد" had their own name prefilled at checkout
// and was then refused by the server. Anything that validates a name or an Egyptian
// mobile should import from here rather than start a tenth variant.
//
// The backend keeps its own copies out of necessity (a client-side rule is a convenience,
// never the enforcement) - see CreateOrderRequest.cs and RegisterRequest.cs, which must
// stay in step with these.

// Arabic (\u0600-\u06FF) as well as Latin letters, plus spaces. This restaurant's
// customers write their names in both.
export const NAME_PATTERN = /^[a-zA-Z\u0600-\u06FF\s]+$/;

// Egyptian mobile numbers: 010, 011, 012 or 015 followed by 8 digits, 11 in total.
// Deliberately stricter than the backend's shared ^[0-9]+$, which also has to accept the
// landlines and foreign numbers that reach the admin "Create Order" screen and the POS
// sync. On a customer-facing form a typo means the driver cannot call and the WhatsApp
// confirmation never arrives, so the stricter rule belongs here.
export const EGYPT_MOBILE_PATTERN = /^(010|011|012|015)\d{8}$/;

// Digits only, with the international form folded back to the local one, capped at the
// 11 digits an Egyptian mobile actually has. Numbers copied out of a WhatsApp contact
// arrive as "+20 101 234 5678" or "00201012345678", which is the same number as
// 01012345678 - rejecting those as "not an Egyptian mobile" would be both wrong and
// baffling. Only an exactly-12-digit 20-prefixed string is rewritten, so someone typing
// their number one digit at a time never trips it.
export function normalizeEgyptMobile(raw: string): string {
  let digits = raw.replace(/\D/g, '');

  if (digits.startsWith('00')) {
    digits = digits.slice(2);
  }
  if (digits.length === 12 && digits.startsWith('20')) {
    digits = `0${digits.slice(2)}`;
  }

  return digits.slice(0, 11);
}
