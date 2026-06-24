# MATGER Workflows

## Checkout Workflow

1. Customer adds products/variants to cart.
2. Customer applies coupon if eligible.
3. Customer starts checkout.
4. System validates address, shipping method, stock, cart state, and prices.
5. System creates order, order items, payment, payment attempt, and inventory reservations.
6. Customer confirms or fails mock payment.
7. Confirmed payment moves the order forward according to the order state rules.

## Pricing Workflow

1. Admin configures base price, cost price, and optional sale window.
2. Public catalog calculates effective price.
3. Checkout snapshots unit price and cost price.
4. Reports use snapshots rather than unstable current prices.
5. Admin can inspect price history.

## Fulfillment Workflow

1. Admin/OrderManager opens picking list.
2. OrderManager moves order to processing.
3. OrderManager marks order shipped.
4. OrderManager marks order delivered.
5. Delivery can trigger loyalty point earning.

## Reorder Planning Workflow

1. Inventory item has supplier, supplier SKU, reorder point, reorder quantity, lead time, and bin location.
2. Admin/InventoryManager opens reorder-needed endpoint.
3. System returns low/critical stock items with suggested quantities.

## Stock Adjustment Approval Workflow

1. InventoryManager creates a stock adjustment request.
2. Admin reviews the request.
3. Approval updates stock and creates an inventory movement.
4. Rejection leaves stock unchanged.
5. Double approval and negative-stock outcomes are blocked.

## Risk Signal Workflow

1. Checkout/order flow evaluates simple risk rules.
2. Signals are recorded without blocking the order automatically.
3. Admin reviews open signals.
4. Admin resolves or dismisses signals with a note.

## Wallet Workflow

1. Admin credits/debits customer wallet with a reason.
2. Balance cannot become negative.
3. Every change creates a wallet transaction.
4. Customer can view only their own wallet and transactions.

## Loyalty Workflow

1. Customer has a loyalty account.
2. Delivered orders can award points.
3. Admin can adjust points.
4. Points cannot become negative.
5. Customer can view only their own loyalty data.
