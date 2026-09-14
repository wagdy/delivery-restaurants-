#!/usr/bin/env python3
"""
Concurrent loyalty redemption test.

LOCAL DEVELOPMENT ONLY. This overwrites a real customer's points balance and
redeems against it. It is hardcoded to localhost:5080 and the local Postgres
container so it cannot reach a deployed environment by accident - keep it that
way. Never point it at production.

Guards LoyaltyService.RedeemPointsAsync against the race it was fixed for.
Sets a customer's balance to a known value, then fires N simultaneous redemption
requests each asking for the FULL balance, all released together by a barrier so
they genuinely overlap inside the server instead of queueing.

Correct behaviour: exactly one succeeds, the balance lands on zero, and the
ledger has exactly one row. Against the pre-fix code every request succeeded -
eight redemptions of 100 points against a 100-point balance - which is what this
exists to stop coming back.

Run it with the API on :5080 and the dev database up:

    python3 backend/tools/loyalty-race-test.py

Exit code 0 means the guard holds.
"""
import json
import subprocess
import sys
import threading
import urllib.error
import urllib.request

API = "http://localhost:5080/api"
CONCURRENCY = 8
BALANCE = 100

DB = ["docker", "exec", "restaurant-delivery-db",
      "psql", "-U", "postgres", "-d", "restaurant_delivery", "-t", "-A", "-c"]


def sql(q):
    return subprocess.run(DB + [q], capture_output=True, text=True).stdout.strip()


def post(path, payload, token=None):
    req = urllib.request.Request(
        API + path,
        data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json",
                 **({"Authorization": "Bearer " + token} if token else {})},
        method="POST")
    try:
        with urllib.request.urlopen(req) as r:
            return r.status, json.loads(r.read() or b"{}")
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"{}")


def main():
    _, auth = post("/auth/login", {"identifier": "admin@restaurant.com",
                                   "password": "Admin@12345"})
    token = auth.get("token")
    if not token:
        print("could not sign in:", auth)
        return 1

    customer_id = sql("""SELECT u."Id" FROM "AspNetUsers" u
                         JOIN "LoyaltyProfiles" p ON p."AppUserId" = u."Id"
                         WHERE u."Role" = 'Customer' AND u."IsDeleted" = false LIMIT 1;""")
    if not customer_id:
        print("no customer with a loyalty profile found")
        return 1

    sql(f"""UPDATE "LoyaltyProfiles" SET "CurrentPoints" = {BALANCE}
            WHERE "AppUserId" = '{customer_id}';""")
    ledger_before = int(sql(f"""SELECT COUNT(*) FROM "LoyaltyPointTransactions"
                                WHERE "CustomerId" = '{customer_id}'
                                AND "TransactionType" = 'Redeemed';"""))

    print(f"customer   : {customer_id}")
    print(f"balance    : {BALANCE}")
    print(f"firing     : {CONCURRENCY} simultaneous redemptions of {BALANCE} points each")
    print()

    barrier = threading.Barrier(CONCURRENCY)
    results = [None] * CONCURRENCY

    def attempt(i):
        barrier.wait()                      # release all threads together
        results[i] = post("/loyalty/redeem",
                          {"customerId": customer_id,
                           "pointsToRedeem": BALANCE,
                           "checkReference": f"race-{i}"},
                          token)

    threads = [threading.Thread(target=attempt, args=(i,)) for i in range(CONCURRENCY)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()

    accepted = sum(1 for s, _ in results if s == 200)
    rejected = CONCURRENCY - accepted

    final = int(sql(f"""SELECT "CurrentPoints" FROM "LoyaltyProfiles"
                        WHERE "AppUserId" = '{customer_id}';"""))
    ledger_after = int(sql(f"""SELECT COUNT(*) FROM "LoyaltyPointTransactions"
                               WHERE "CustomerId" = '{customer_id}'
                               AND "TransactionType" = 'Redeemed';"""))
    ledger_rows = ledger_after - ledger_before
    redeemed_total = accepted * BALANCE

    print(f"accepted   : {accepted}")
    print(f"rejected   : {rejected}")
    print(f"balance    : {BALANCE} -> {final}")
    print(f"ledger rows: {ledger_rows}")
    print(f"points out : {redeemed_total} (customer only ever had {BALANCE})")
    print()

    ok = True
    if accepted != 1:
        print(f"FAIL  {accepted} redemptions succeeded against a single {BALANCE}-point balance")
        ok = False
    if final < 0:
        print(f"FAIL  balance went negative ({final})")
        ok = False
    if ledger_rows != accepted:
        print(f"FAIL  ledger rows ({ledger_rows}) do not match accepted redemptions ({accepted})")
        ok = False
    if ok:
        print("PASS  exactly one redemption succeeded, balance 0, ledger consistent")
    return 0 if ok else 2


if __name__ == "__main__":
    sys.exit(main())
