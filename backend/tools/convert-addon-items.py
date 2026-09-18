#!/usr/bin/env python3
"""
Convert menu items that are really add-ons ("اضافة طحينة", "اضافة نوتيلا", ...) into
real AddOn catalog entries attached to the dishes they belong to.

WHY THIS IS PHASED AND NOT ONE BUTTON
-------------------------------------
The obvious automation - "attach each add-on to the dishes in its own category" -
produces nonsense on this menu, because the categories are inverted:

  * "Dessert" holds the SAVOURY add-ons (tahini, pastrami, salami, mozzarella,
    jalapeno, meat) next to cheesecake and baklava.
  * "Grilled trays" holds the DESSERT toppings (Nutella, Lotus, marshmallow,
    ice cream, caramel) next to 3150 EGP grill platters.

Attaching by category would put tahini on cheesecake and marshmallow on a grill
platter. Which add-on belongs to which dish is a menu decision, so this script
does the mechanical half and asks a human for the half that needs judgement.

PHASES
------
  export   read-only. Writes a worksheet of every add-on-shaped menu item.
           You fill in the `attach_to_ids` column. Nothing is written.
  create   creates the AddOn catalog entries from the worksheet. Idempotent:
           an add-on whose name already exists is skipped, not duplicated.
  attach   attaches each created add-on to the dish ids you listed.
  retire   marks the old standalone items unavailable. NOT a delete - see below.

RETIRE, NOT DELETE
------------------
OrderItem -> MenuItem is DeleteBehavior.Restrict, so deleting an item that appears
on any historical order is refused by the database (MenuItemService.DeleteAsync
turns that into "Cannot delete ... referenced by existing orders"). Marking them
unavailable takes them off the storefront, keeps order history intact, and is one
toggle to undo if a mapping turns out wrong. Delete them by hand later if you want,
once you have watched a few services go by without them.

EVERY PHASE IS DRY-RUN UNTIL YOU PASS --apply.

  python3 convert-addon-items.py --phase export
  python3 convert-addon-items.py --phase create            # dry run
  python3 convert-addon-items.py --phase create  --apply
  python3 convert-addon-items.py --phase attach --apply
  python3 convert-addon-items.py --phase retire --apply

Auth: set OTANTIK_TOKEN to an admin JWT. The script never asks for or stores a
password. Get one with your own login, e.g.

  curl -s -X POST "$API/auth/login" -H 'Content-Type: application/json' \
    -d '{"identifier":"<your admin phone>","password":"<your password>"}' \
    | python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])'
"""

import argparse
import csv
import json
import os
import re
import sys
import urllib.error
import urllib.request

DEFAULT_API = "https://api-production-0b83e.up.railway.app/api"
WORKSHEET = "addon-conversion-worksheet.csv"

# Anchored deliberately: a dish merely CONTAINING the word is not an add-on. Checked
# against the live menu - anchored and unanchored both matched the same 46 items, so
# this is not silently dropping any.
ADDON_NAME = re.compile(r"^\s*(اضاف|إضاف|extra\b)", re.I)


def call(method, path, token=None, data=None, api=DEFAULT_API):
    req = urllib.request.Request(
        api + path,
        data=json.dumps(data).encode() if data is not None else None,
        method=method,
    )
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as resp:
            body = resp.read()
            return resp.status, (json.loads(body) if body else None)
    except urllib.error.HTTPError as e:
        raw = e.read()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw[:300].decode("utf-8", "replace")


def fetch_menu(api):
    status, data = call("GET", "/menuitems?isAvailable=true", api=api)
    if status != 200:
        sys.exit(f"Could not read the menu ({status}): {data}")
    return data if isinstance(data, list) else data.get("items", data)


def split_menu(items):
    addons = [i for i in items if ADDON_NAME.search(i["name"])]
    dishes = [i for i in items if not ADDON_NAME.search(i["name"])]
    return addons, dishes


# --------------------------------------------------------------------------- export
def phase_export(api, _token, _apply):
    items = fetch_menu(api)
    addons, dishes = split_menu(items)

    with open(WORKSHEET, "w", newline="", encoding="utf-8-sig") as fh:
        w = csv.writer(fh)
        w.writerow(["menu_item_id", "name", "price", "current_category", "attach_to_ids", "skip"])
        for a in sorted(addons, key=lambda x: (x["category"], x["name"])):
            w.writerow([a["id"], a["name"], a["price"], a["category"], "", ""])

    ref = "dish-reference.csv"
    with open(ref, "w", newline="", encoding="utf-8-sig") as fh:
        w = csv.writer(fh)
        w.writerow(["dish_id", "name", "price", "category"])
        for d in sorted(dishes, key=lambda x: (x["category"], x["name"])):
            w.writerow([d["id"], d["name"], d["price"], d["category"]])

    print(f"Wrote {WORKSHEET} ({len(addons)} add-on-shaped items)")
    print(f"Wrote {ref} ({len(dishes)} real dishes to choose from)\n")
    print("Fill in attach_to_ids with semicolon-separated dish ids, e.g. 104;113;125")
    print("Leave it blank to create the add-on but attach it to nothing yet.")
    print("Put any value in `skip` to leave that row alone entirely.")


def read_worksheet():
    if not os.path.exists(WORKSHEET):
        sys.exit(f"{WORKSHEET} not found - run `--phase export` first.")
    rows = []
    with open(WORKSHEET, newline="", encoding="utf-8-sig") as fh:
        for r in csv.DictReader(fh):
            if (r.get("skip") or "").strip():
                continue
            rows.append(r)
    return rows


# --------------------------------------------------------------------------- create
def phase_create(api, token, apply_changes):
    rows = read_worksheet()
    status, existing = call("GET", "/addons", token=token, api=api)
    if status != 200:
        sys.exit(f"Could not read add-ons ({status}): {existing}")
    by_name = {a["name"].strip(): a for a in existing}

    created = skipped = failed = 0
    for r in rows:
        name = r["name"].strip()
        price = float(r["price"])
        if name in by_name:
            print(f"  skip   {name!r} - already an add-on (id {by_name[name]['id']})")
            skipped += 1
            continue
        if not apply_changes:
            print(f"  WOULD CREATE  {name!r} at {price}")
            created += 1
            continue
        st, res = call("POST", "/addons", token=token, data={"name": name, "price": price}, api=api)
        if st in (200, 201):
            print(f"  created {name!r} -> add-on id {res['id']}")
            created += 1
        else:
            print(f"  FAILED  {name!r}: {st} {res}")
            failed += 1

    print(f"\n{'created' if apply_changes else 'would create'}: {created}   skipped: {skipped}   failed: {failed}")


# --------------------------------------------------------------------------- attach
def phase_attach(api, token, apply_changes):
    rows = read_worksheet()
    status, addons = call("GET", "/addons", token=token, api=api)
    if status != 200:
        sys.exit(f"Could not read add-ons ({status}): {addons}")
    addon_id_by_name = {a["name"].strip(): a["id"] for a in addons}

    # dish id -> the add-on ids it should gain
    wanted = {}
    for r in rows:
        ids = [x.strip() for x in (r.get("attach_to_ids") or "").split(";") if x.strip()]
        if not ids:
            continue
        aid = addon_id_by_name.get(r["name"].strip())
        if aid is None:
            print(f"  WARN   {r['name']!r} has no add-on yet - run `--phase create` first. Skipping.")
            continue
        for d in ids:
            wanted.setdefault(int(d), set()).add(aid)

    if not wanted:
        # Distinguish "you filled nothing in" from "everything you filled in was
        # skipped" - the second is a real problem and the first is not.
        any_ids = any((r.get("attach_to_ids") or "").strip() for r in rows)
        if any_ids:
            print("\nNothing to attach: every mapping was skipped (see the warnings above).")
        else:
            print("Nothing to attach: no attach_to_ids filled in.")
        return

    menu = {i["id"]: i for i in fetch_menu(api)}
    changed = failed = 0
    for dish_id, add_ids in sorted(wanted.items()):
        dish = menu.get(dish_id)
        if dish is None:
            print(f"  WARN   dish id {dish_id} is not on the live menu. Skipping.")
            continue
        current = {a["id"] for a in dish["addOns"]}
        merged = sorted(current | add_ids)
        if merged == sorted(current):
            print(f"  skip   {dish['name']!r} already has those add-ons")
            continue
        if not apply_changes:
            print(f"  WOULD ATTACH {sorted(add_ids)} to {dish['name']!r} (had {sorted(current)})")
            changed += 1
            continue
        # PUT replaces the whole item, so every field is echoed back deliberately -
        # sending a partial body here would blank description/image/sub-category.
        body = {
            "name": dish["name"],
            "nameAr": dish.get("nameAr"),
            "description": dish.get("description"),
            "price": dish["price"],
            "category": dish["category"],
            "subCategoryId": dish.get("subCategoryId"),
            "imageUrl": dish.get("imageUrl"),
            "isAvailable": dish["isAvailable"],
            "addOnIds": merged,
        }
        st, res = call("PUT", f"/menuitems/{dish_id}", token=token, data=body, api=api)
        if st in (200, 201):
            print(f"  attached {sorted(add_ids)} to {dish['name']!r}")
            changed += 1
        else:
            print(f"  FAILED  {dish['name']!r}: {st} {res}")
            failed += 1

    print(f"\n{'attached' if apply_changes else 'would attach'} on {changed} dishes   failed: {failed}")


# --------------------------------------------------------------------------- retire
def phase_retire(api, token, apply_changes):
    rows = read_worksheet()
    menu = {i["id"]: i for i in fetch_menu(api)}
    done = failed = 0
    for r in rows:
        mid = int(r["menu_item_id"])
        item = menu.get(mid)
        if item is None:
            print(f"  skip   id {mid} ({r['name']!r}) is already off the available menu")
            continue
        if not apply_changes:
            print(f"  WOULD RETIRE  {item['name']!r} (id {mid})")
            done += 1
            continue
        body = {
            "name": item["name"],
            "nameAr": item.get("nameAr"),
            "description": item.get("description"),
            "price": item["price"],
            "category": item["category"],
            "subCategoryId": item.get("subCategoryId"),
            "imageUrl": item.get("imageUrl"),
            "isAvailable": False,
            "addOnIds": [a["id"] for a in item["addOns"]],
        }
        st, res = call("PUT", f"/menuitems/{mid}", token=token, data=body, api=api)
        if st in (200, 201):
            print(f"  retired {item['name']!r}")
            done += 1
        else:
            print(f"  FAILED  {item['name']!r}: {st} {res}")
            failed += 1

    print(f"\n{'retired' if apply_changes else 'would retire'}: {done}   failed: {failed}")
    if apply_changes and done:
        print("These are hidden, not deleted. Flip isAvailable back on in the admin to undo.")


PHASES = {"export": phase_export, "create": phase_create, "attach": phase_attach, "retire": phase_retire}


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--phase", required=True, choices=PHASES)
    p.add_argument("--apply", action="store_true", help="actually write. Without it, every phase is a dry run.")
    p.add_argument("--api", default=os.environ.get("OTANTIK_API", DEFAULT_API))
    args = p.parse_args()

    token = os.environ.get("OTANTIK_TOKEN")
    if args.phase != "export" and not token:
        sys.exit("Set OTANTIK_TOKEN to an admin JWT first - see the header of this file.")

    if args.apply:
        print(f"*** APPLYING to {args.api} ***\n")
    elif args.phase != "export":
        print(f"--- dry run against {args.api} (pass --apply to write) ---\n")

    PHASES[args.phase](args.api, token, args.apply)


if __name__ == "__main__":
    main()
