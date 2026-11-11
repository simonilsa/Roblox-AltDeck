"""
Builds the AltDeck website: one page per language, with search-engine tags, a sitemap and robots.txt.

GitHub runs this automatically (see .github/workflows/pages.yml). To try it on your own PC:
    python website/build.py --out _site
then open _site/index.html in a browser.

Edit the texts in website/i18n/<language>.json. The app's own translations (src/Languages)
are reused for the little AltDeck window in the picture, and src/Flags for the flags.
"""
import argparse
import html
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SITE = ROOT / "website"
ORDER = ["en", "es", "pt", "fil", "id", "fr", "de", "ru", "tr", "vi"]
HREFLANG = {"fil": "tl"}  # search engines know Filipino/Tagalog as "tl"
OG_LOCALE = {"en": "en_US", "es": "es_ES", "pt": "pt_BR", "fil": "fil_PH", "id": "id_ID",
             "fr": "fr_FR", "de": "de_DE", "ru": "ru_RU", "tr": "tr_TR", "vi": "vi_VN"}


def load_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base-url", default="https://YOUR-GITHUB-NAME.github.io/AltDeck")
    ap.add_argument("--repo-url", default="https://github.com/YOUR-GITHUB-NAME/AltDeck")
    ap.add_argument("--out", default="_site")
    args = ap.parse_args()

    base_url = args.base_url.rstrip("/")
    repo_url = args.repo_url.rstrip("/")
    out = Path(args.out)
    if out.exists():
        shutil.rmtree(out)
    (out / "assets" / "flags").mkdir(parents=True)

    config = load_json(SITE / "config.json") if (SITE / "config.json").exists() else {}
    template = (SITE / "template.html").read_text(encoding="utf-8")

    # Languages that have both website texts and app texts.
    site_text = {p.stem: load_json(p) for p in (SITE / "i18n").glob("*.json")}
    app_text = {p.stem: load_json(p) for p in (ROOT / "src" / "Languages").glob("*.json")}
    codes = [c for c in ORDER if c in site_text and c in app_text]
    codes += sorted(c for c in site_text if c in app_text and c not in codes)
    en_site, en_app = site_text["en"], app_text["en"]

    def page_path(code):            # where a language's page lives, relative to the site root
        return "" if code == "en" else f"{code}/"

    def url(code):
        return f"{base_url}/{page_path(code)}"

    urls = {
        "repo_url": repo_url,
        "download_url": f"{repo_url}/releases/latest/download/AltDeck.exe",
        "releases_url": f"{repo_url}/releases",
        "security_url": f"{repo_url}/blob/main/SECURITY.md",
        "contributing_url": f"{repo_url}/blob/main/CONTRIBUTING.md",
    }

    hreflang = "\n".join(
        [f'<link rel="alternate" hreflang="{HREFLANG.get(c, c)}" href="{url(c)}">' for c in codes]
        + [f'<link rel="alternate" hreflang="x-default" href="{url("en")}">'])

    verification = ""
    if config.get("google_site_verification"):
        verification = f'<meta name="google-site-verification" content="{html.escape(config["google_site_verification"])}">'

    screenshots = sorted(p.name for p in (SITE / "assets").glob("screenshot-*.png"))

    for code in codes:
        s = {**en_site, **site_text[code]}
        a = {**en_app, **app_text[code]}
        root = "" if code == "en" else "../"

        def t(key):
            return html.escape(s[key], quote=True)

        lang_menu = "\n".join(
            f'          <li><a href="{(root + page_path(c)) or "./"}" hreflang="{c}" lang="{c}" data-lang="{c}"'
            f'{" aria-current=\"page\"" if c == code else ""}>'
            f'<img class="flag" src="{root}assets/flags/{c}.png" alt="" width="24" height="16">'
            f'{html.escape(app_text[c]["_name"])}</a></li>'
            for c in codes)

        lang_flags = " ".join(
            f'<span lang="{c}"><img class="flag" src="{root}assets/flags/{c}.png" alt="" width="21" height="14">'
            f'{html.escape(app_text[c]["_name"])}</span>'
            for c in codes)

        shots_html = "\n".join(
            f'      <figure><img src="{root}assets/{name}" alt="{html.escape(s["shots.alt"].replace("{0}", str(i + 1)))}" loading="lazy"></figure>'
            for i, name in enumerate(screenshots))

        suggest = {
            c: {
                "text": {**en_site, **site_text[c]}["banner.text"],
                "go": {**en_site, **site_text[c]}["banner.go"],
                "url": (root + page_path(c)) or "./",
                "flag": f"{root}assets/flags/{c}.png",
            }
            for c in codes
        }

        jsonld = {
            "@context": "https://schema.org",
            "@type": "SoftwareApplication",
            "name": "AltDeck",
            "description": s["meta.description"],
            "url": url(code),
            "downloadUrl": urls["download_url"],
            "operatingSystem": "Windows 10, Windows 11",
            "applicationCategory": "UtilitiesApplication",
            "isAccessibleForFree": True,
            "license": "https://opensource.org/licenses/MIT",
            "offers": {"@type": "Offer", "price": "0", "priceCurrency": "EUR"},
            "inLanguage": codes,
            "image": f"{base_url}/assets/social-preview.png",
        }

        raw = {
            "lang": code,
            "lang_code": code,
            "lang_name": html.escape(a["_name"]),
            "lang_label": html.escape(a.get("lang.label", "Language")),
            "canonical": url(code),
            "base_url": base_url,
            "hreflang": hreflang,
            "og_locale": OG_LOCALE.get(code, code),
            "verification": verification,
            "root": root,
            "home": root or "./",
            "lang_menu": lang_menu,
            "lang_flags": lang_flags,
            "screenshots": shots_html,
            "suggest_data": json.dumps(suggest, ensure_ascii=False).replace("</", "<\\/"),
            "jsonld": json.dumps(jsonld, ensure_ascii=False).replace("</", "<\\/"),
            "mock_selected": html.escape(a["step2.some"].replace("{0}", "4")),
            "mock_friend": html.escape(a["step3.friend"].replace("{0}", "Alex")),
            "mock_launch": html.escape(a["dest.launch"].strip()),
            **urls,
        }

        page = template
        for key, value in raw.items():
            page = page.replace("{{{" + key + "}}}", value)
        for key in ("step1.title", "step2.title", "step3.title", "status.multiOn"):
            page = page.replace("{{app." + key + "}}", html.escape(a[key].strip()))
        for key in s:
            page = page.replace("{{" + key + "}}", t(key))

        if "{{" in page:
            start = page.index("{{")
            raise SystemExit(f"[{code}] unfilled placeholder near: {page[start:start + 60]}")

        target = out / page_path(code) / "index.html"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(page, encoding="utf-8")

    # Assets
    shutil.copy(SITE / "style.css", out / "assets" / "style.css")
    for p in (SITE / "assets").iterdir():
        if p.is_file():
            shutil.copy(p, out / "assets" / p.name)
    for c in codes:
        flag = ROOT / "src" / "Flags" / f"{c}.png"
        if flag.exists():
            shutil.copy(flag, out / "assets" / "flags" / flag.name)

    # Sitemap + robots
    alt = "".join(
        f'\n    <xhtml:link rel="alternate" hreflang="{HREFLANG.get(c, c)}" href="{url(c)}"/>' for c in codes)
    entries = "".join(f"\n  <url>\n    <loc>{url(c)}</loc>{alt}\n  </url>" for c in codes)
    (out / "sitemap.xml").write_text(
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">'
        f"{entries}\n</urlset>\n", encoding="utf-8")
    (out / "robots.txt").write_text(f"User-agent: *\nAllow: /\nSitemap: {base_url}/sitemap.xml\n", encoding="utf-8")

    print(f"Built {len(codes)} languages into {out}: {', '.join(codes)}")


if __name__ == "__main__":
    main()
