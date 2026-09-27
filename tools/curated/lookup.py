import json, sys, urllib.request, urllib.parse, time
sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")
def q(sheet, name, fields):
    query = name[2:] if name.startswith("Q:") else f'Name~"{name}"'
    url = "https://v2.xivapi.com/api/search?" + urllib.parse.urlencode({"sheets": sheet, "query": query, "fields": fields, "limit": 50})
    for attempt in range(3):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "Tsukimichi-curated-seed/0.1 (dalamud plugin datagen)"})
            with urllib.request.urlopen(req, timeout=30) as r:
                return json.load(r)
        except Exception as e:
            print("  ERR", repr(e)); time.sleep(1.5)
    return {"results": [], "error": "failed"}
sheet = sys.argv[1]
if sheet == "Quest":
    fields = "Name,Id,Expansion.Name,ClassJobLevel@as(raw),IssuerLocation.Territory.PlaceName.Name"
else:
    fields = "Name,ContentType.Name,ClassJobLevelRequired"
names = [l.strip() for l in sys.stdin if l.strip()]
for n in names:
    d = q(sheet, n, fields)
    print(f"## version={d.get('version')} search={n!r}")
    for r in d.get("results", []):
        f = r["fields"]
        if sheet == "Quest":
            loc = ((f.get("IssuerLocation") or {}).get("fields") or {}).get("Territory", {}).get("fields", {}).get("PlaceName", {}).get("fields", {}).get("Name")
            print(f"  {r['row_id']}\t{f['Name']}\t{f['Id']}\t{f['Expansion']['fields']['Name']}\tLv{f['ClassJobLevel@as(raw)'][0]}\t{loc}")
        else:
            print(f"  {r['row_id']}\t{f['Name']}\t{f['ContentType']['fields'].get('Name')}\tLv{f.get('ClassJobLevelRequired')}")
    time.sleep(0.15)
