// Mirrors CustomersController.Register's request/response shape (api/customers/register) -
// kept separate from customer-analytics.model.ts, which is the CRM/Customer Insights
// screen's own, differently-shaped model.
export interface RegisterCustomerRequest {
  customerName: string;
  phone: string;
}

export interface FindOrCreateCustomerResult {
  customerId: string;
  // False when this phone number was already registered - the existing account was
  // returned instead of creating a new one, with no welcome bonus/WhatsApp re-sent.
  isNewCustomer: boolean;
}
