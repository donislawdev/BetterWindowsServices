#!/usr/bin/env python3
"""Say which NuGet packages of this repository have a newer version nobody proposed.

    python .github/scripts/outdated_gate.py selftest
    python .github/scripts/outdated_gate.py canary
    python .github/scripts/outdated_gate.py verdict packages.json --held .github/outdated/held.toml \
        [--summary FILE]

.github/workflows/outdated.yml runs the three in that order. Each one exits 0 when
it is satisfied, 1 when it found what it exists to find, and 2 when it could not
read what it was given - and 2 is never a pass.

Why this exists
---------------
Dependabot is configured to watch every NuGet package here, and for most of them
it never will. Its NuGet updater decides whether a newer version is compatible
with the project before proposing it, and it answers "not compatible" for every
package that carries an assembly when the project targets
net10.0-windows10.0.17763. The run of 2026-11-02 logged exactly that for CsCheck
4.9.1, CsCheck 5.0.0 and WPF-UI 4.3.0, then "No updatable version found", and
went green. Every NuGet pull request it ever opened here moved one of two
packages that carry no assembly at all, an analyser and a source generator.
CsCheck 5.0.0 was moved by hand in pull request 105 because nothing said it
existed. So this check asks NuGet itself, once a week, and is red when the tree
is behind a version old enough that Dependabot would have proposed it.

What is read, and why none of it is trusted to be complete
-----------------------------------------------------------
The packages come from `dotnet list package --format json` after a restore,
which is what NuGet resolved rather than what a file seems to say. Two checks
keep that list honest. Every project git tracks has to appear in it, so a
project the solution forgot is a refusal rather than a shorter list. And the
PackageReference items the repository's own files name have to appear in it:

  - one written in a project file, in the listing of THAT project, so a package
    one project lost is a refusal even while another project still lists it.
  - one written in a props or targets file, somewhere in the listing. Which
    projects import such a file is MSBuild's evaluation, and this gate does not
    repeat it.

The files are read as XML, so a reference inside a comment is not a reference.
A reference whose Condition is false for every framework is read as one all the
same, and comes back as a refusal naming it - loud, and the fix is in the file.

The versions come from NuGet's registration resource, found through the service
index rather than written here. A package with many versions is split into pages
that have to be fetched one by one, and a page that was not read would make a
package look current. So the count of pages the index names, and the count of
versions each page names, have to match what was read, and the version in the
tree has to be among the versions read.

Only NuGet is asked. Every address this gate opens - the service index, the
registration, every page the registration names, every redirect - has to be
HTTPS on the host of the service index, so a crafted answer cannot point the job
at a file on the runner or at another host.

What counts as newer
--------------------
A listed version that sorts above the one in the tree, in NuGet's order: up to
four numbers, then a release above any prerelease of it, prerelease labels
compared part by part, numbers below words. Prereleases count only when the tree
is on a prerelease of that package itself, which is how `dotnet list package
--outdated` decides it too.

The cooldown, and where its number lives
-----------------------------------------
A version younger than the cooldown is reported as cooling and is not red, so
that this check asks for exactly what Dependabot would have proposed and nothing
earlier. The number of days is read from the nuget entry of
.github/dependabot.yml rather than repeated here, because two copies of one
number drift, and the day they disagree one of them is wrong without saying so.

Held versions
-------------
A newer version that the owner decided not to take yet goes into
.github/outdated/held.toml with the highest version the decision covers, a date
it expires and the reason. A newer version past that, or the date passing, makes
the package red again. A hold that covers nothing any more - the package moved,
or left the tree - is red as well, because a hold nobody needs still reads as a
decision somebody made.

The canary
----------
Before the verdict trusts its own reading, Newtonsoft.Json 12.0.3 goes through
the same path and has to come out behind, past the cooldown, on a major step.
Version 13 has been on NuGet since 2021. Without that, a green verdict cannot
tell a current tree from a registration that answered nothing, or a comparison
that never says newer.
"""
import argparse
import datetime
import http.client
import io
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import tomllib
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ElementTree
import zlib

SERVICE_INDEX = "https://api.nuget.org/v3/index.json"

# The one host this gate talks to, taken from the address above rather than written twice.
NUGET_HOST = urllib.parse.urlsplit(SERVICE_INDEX).hostname

# The registration resource that lists every version with its publication date, compressed,
# SemVer 2 versions included. Found by this type in the service index on every run.
REGISTRATION_TYPE = "RegistrationsBaseUrl/3.6.0"

# Old on purpose, for the reason osv_gate.py gives about its own canaries: an answer years old
# cannot be withdrawn by a registry changing its mind about something recent.
CANARIES = (("Newtonsoft.Json", "12.0.3", "major"),)

# A ceiling on any one answer and on any one file, far above anything real. The largest
# registration page this check reads is in the hundreds of kilobytes, the listing in the tens.
# The ceiling applies after decompression too, so a small compressed answer cannot unpack
# into something that fills the runner.
MOST_BYTES = 32 * 1024 * 1024

ATTEMPTS = 3

PACKAGE_ID = re.compile(r"^[A-Za-z0-9_][A-Za-z0-9._-]{0,99}$")
VERSION = re.compile(
    r"^(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$")

HOLD_KEYS = {"package", "through", "until", "reason"}

# The order a table is read in: what needs doing first.
STATE_ORDER = {"behind": 0, "held": 1, "cooling": 2, "current": 3}


class Malformed(Exception):
    """An input whose shape is not the one this gate reads. Always a 2, never a pass."""


class Unreadable(Exception):
    """An answer that could not be fetched or read. Always a 2, never a pass."""


def refused(exc):
    print("REFUSED: %s" % exc)
    return 2


def one_line(text):
    """Text for a table cell or a log line: whitespace collapsed, newlines included."""
    return " ".join(text.split())


# ---- versions -----------------------------------------------------------------------------------


class Version:
    """A NuGet version, ordered the way NuGet orders them."""

    def __init__(self, text):
        match = VERSION.match(text.strip()) if isinstance(text, str) else None
        if match is None:
            raise Malformed("%r is not a NuGet version" % (text,))
        self.text = text.strip()
        self.release = tuple(int(part or 0) for part in match.groups()[:4])
        labels = match.group(5)
        self.pre = tuple(labels.split(".")) if labels else ()
        self.key = (self.release, 0 if self.pre else 1,
                    tuple((0, int(label), "") if label.isdigit() else (1, 0, label.lower())
                          for label in self.pre))

    def __eq__(self, other):
        return self.key == other.key

    def __lt__(self, other):
        return self.key < other.key

    def __le__(self, other):
        return self.key <= other.key

    def __hash__(self):
        return hash(self.key)

    def __repr__(self):
        return self.text


def step(old, new):
    """How far apart two versions are, by the first number that differs."""
    for name, a, b in zip(("major", "minor", "patch", "revision"), old.release, new.release):
        if a != b:
            return name
    return "prerelease"


# ---- answers from outside -------------------------------------------------------------------------


def checked_url(url):
    """The URL itself, when this gate may open it: HTTPS, on NuGet's host, nothing else in it."""
    try:
        parts = urllib.parse.urlsplit(url) if isinstance(url, str) else None
        port = parts.port if parts is not None else None
    except ValueError:
        parts = None
        port = None
    if (parts is None or parts.scheme != "https" or parts.hostname != NUGET_HOST
            or parts.username or parts.password or port not in (None, 443)):
        raise Unreadable("%r is not an address on %s over HTTPS, so it is not opened" % (url, NUGET_HOST))
    return url


class CheckedRedirects(urllib.request.HTTPRedirectHandler):
    """Follows a redirect only to an address checked_url accepts."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        checked_url(newurl)
        return super().redirect_request(req, fp, code, msg, headers, newurl)


OPENER = urllib.request.build_opener(CheckedRedirects)


def decompress(body):
    inflater = zlib.decompressobj(16 + zlib.MAX_WBITS)
    out = inflater.decompress(body, MOST_BYTES + 1)
    if len(out) > MOST_BYTES or inflater.unconsumed_tail:
        raise Unreadable("an answer unpacks to more than %d bytes" % MOST_BYTES)
    if not inflater.eof:
        raise Unreadable("an answer ends before its compressed stream does")
    return out


def fetch_json(url, open_url=None, sleep=time.sleep):
    """One JSON answer from NuGet, retried on a transient failure, refused on anything else."""
    checked_url(url)
    open_url = open_url or OPENER.open
    last = None
    for attempt in range(ATTEMPTS):
        try:
            request = urllib.request.Request(url, headers={
                "Accept-Encoding": "gzip",
                "User-Agent": "BetterWindowsServices-outdated-gate",
            })
            with open_url(request, timeout=30) as response:
                body = response.read(MOST_BYTES + 1)
                encoding = (response.headers.get("Content-Encoding") or "").lower()
            if len(body) > MOST_BYTES:
                raise Unreadable("%s: more than %d bytes" % (url, MOST_BYTES))
            if encoding == "gzip":
                body = decompress(body)
            return json.loads(body.decode("utf-8"))
        except urllib.error.HTTPError as exc:
            # An HTTPError is a response as well and holds its connection until it is closed.
            # Closed here on every path, and only its code is kept for the message.
            code = exc.code
            exc.close()
            if code < 500 and code != 429:
                raise Unreadable("%s: HTTP %d" % (url, code)) from None
            last = "HTTP %d" % code
        except (urllib.error.URLError, TimeoutError, ConnectionError, http.client.HTTPException) as exc:
            last = str(exc) or type(exc).__name__
        except (UnicodeDecodeError, json.JSONDecodeError, zlib.error) as exc:
            raise Unreadable("%s: %s" % (url, exc)) from exc
        if attempt + 1 < ATTEMPTS:
            sleep(2 ** attempt * 5)
    raise Unreadable("%s: %s after %d attempts" % (url, last, ATTEMPTS))


def field(record, key, kind, where):
    if not isinstance(record, dict) or key not in record:
        raise Malformed("%s has no %r" % (where, key))
    value = record[key]
    # True is an int to Python. Nothing this gate reads as a number is ever written as true.
    if not isinstance(value, kind) or (kind is int and isinstance(value, bool)):
        raise Malformed("%s: %r is %s, not %s" % (where, key, type(value).__name__, kind.__name__))
    return value


def items(record, key, where, required=False):
    if not isinstance(record, dict):
        raise Malformed("%s is not an object" % where)
    if key not in record:
        if required:
            raise Malformed("%s has no %r" % (where, key))
        return []
    value = record[key]
    if not isinstance(value, list):
        raise Malformed("%s: %r is not a list" % (where, key))
    return value


def registration_base(fetch):
    index = fetch(SERVICE_INDEX)
    for resource in items(index, "resources", "the service index", required=True):
        if isinstance(resource, dict) and resource.get("@type") == REGISTRATION_TYPE:
            base = checked_url(field(resource, "@id", str, "the registration resource"))
            return base if base.endswith("/") else base + "/"
    raise Malformed("the service index names no %s" % REGISTRATION_TYPE)


class Release:
    def __init__(self, version, listed, published):
        self.version = version
        self.listed = listed
        self.published = published


def published_at(text, where):
    try:
        moment = datetime.datetime.fromisoformat(text)
    except (TypeError, ValueError) as exc:
        raise Malformed("%s: %r is not a time" % (where, text)) from exc
    if moment.tzinfo is None:
        moment = moment.replace(tzinfo=datetime.timezone.utc)
    return moment


def counted(record, leaves, where):
    """Refuse a list shorter or longer than the count the same answer gives for it."""
    advertised = field(record, "count", int, where)
    if advertised != len(leaves):
        raise Unreadable("%s names %d entries and %d were read" % (where, advertised, len(leaves)))


def releases_of(package, base, fetch):
    """Every version NuGet has of one package, each page fetched when it is not inline."""
    if not PACKAGE_ID.match(package):
        raise Malformed("%r is not a package id" % package)
    index = fetch(checked_url("%s%s/index.json" % (base, package.lower())))
    pages = items(index, "items", "the registration of " + package, required=True)
    counted(index, pages, "the registration of " + package)
    found = []
    for number, page in enumerate(pages):
        where = "page %d of %s" % (number, package)
        if "items" in page:
            leaves = items(page, "items", where)
        else:
            fetched = fetch(checked_url(field(page, "@id", str, where)))
            leaves = items(fetched, "items", where, required=True)
        counted(page, leaves, where)
        for leaf in leaves:
            entry = field(leaf, "catalogEntry", dict, where)
            version = Version(field(entry, "version", str, where))
            listed = entry.get("listed", True)
            if not isinstance(listed, bool):
                raise Malformed("%s: listed is not true or false" % where)
            found.append(Release(version, listed, published_at(entry.get("published"), where)))
    if not found:
        raise Unreadable("NuGet listed no version of %s at all" % package)
    return found


# ---- what the repository says -----------------------------------------------------------------------


def read_text(path, limit=MOST_BYTES):
    """A file of the repository as text, refused past the ceiling rather than read whole."""
    with open(path, "rb") as stream:
        data = stream.read(limit + 1)
    if len(data) > limit:
        raise Unreadable("%s is larger than %d bytes" % (path, limit))
    try:
        return data.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise Unreadable("%s is not UTF-8: %s" % (path, exc)) from exc


def repository_root():
    out = subprocess.run(["git", "rev-parse", "--show-toplevel"], check=True, capture_output=True, text=True)
    return out.stdout.strip()


def git_files(root, *patterns):
    out = subprocess.run(["git", "ls-files", "--", *patterns], check=True, capture_output=True, text=True,
                         cwd=root)
    return [line for line in out.stdout.splitlines() if line]


def cooldown_days(text):
    """The default-days of the one nuget entry in a dependabot.yml, refused if not exactly one."""
    blocks = re.split(r"(?m)^\s*-\s*package-ecosystem:\s*", text)
    nuget = [block for block in blocks[1:] if re.match(r"[\"']?nuget[\"']?\s*$", block.splitlines()[0])]
    if len(nuget) != 1:
        raise Malformed("dependabot.yml has %d nuget entries, not one" % len(nuget))
    days = re.findall(r"(?m)^\s*default-days:\s*(\d+)\s*$", nuget[0])
    if len(days) != 1:
        raise Malformed("the nuget entry of dependabot.yml has %d default-days, not one" % len(days))
    return int(days[0])


def references(text, where):
    """The packages one MSBuild file names in a PackageReference, by lower-case id.

    Read as XML, so a reference inside a comment is not one. A document type declaration is
    refused before parsing - no project file needs one, and it is the only way to make the
    parser expand entities."""
    if "<!DOCTYPE" in text:
        raise Malformed("%s declares a document type, which no MSBuild file here needs" % where)
    try:
        root = ElementTree.fromstring(text)
    except ElementTree.ParseError as exc:
        raise Malformed("%s is not well-formed XML: %s" % (where, exc)) from exc
    found = {}
    for element in root.iter():
        # The local name, so that an old-style project with the MSBuild namespace reads the same.
        if isinstance(element.tag, str) and element.tag.rsplit("}", 1)[-1] == "PackageReference":
            name = element.get("Include")
            if name:
                found[name.lower()] = name
    return found


def tree(listing, root, tracked, own, shared):
    """{(id, version): projects} from a dotnet list package listing, refused when incomplete.

    own holds what each project file names, by its path from the root. shared holds what the
    props and targets files name, which has to be somewhere in the listing."""
    if not isinstance(listing, dict) or listing.get("version") != 1 or isinstance(listing.get("version"), bool):
        raise Malformed("the listing is not version 1 of the dotnet list package format")
    problems = list(items(listing, "problems", "the listing"))
    for project in items(listing, "projects", "the listing", required=True):
        problems += items(project, "problems", "a project")
    if problems:
        raise Unreadable("dotnet list reported a problem: %s" % json.dumps(problems[0]))
    found = {}
    listed_by = {}
    for project in items(listing, "projects", "the listing", required=True):
        path = field(project, "path", str, "a project")
        name = os.path.relpath(path, root).replace(os.sep, "/")
        listed_by.setdefault(name, set())
        for framework in items(project, "frameworks", name):
            for package in items(framework, "topLevelPackages", name):
                package_id = field(package, "id", str, name)
                resolved = field(package, "resolvedVersion", str, name + " " + package_id)
                found.setdefault((package_id, resolved), set()).add(name)
                listed_by[name].add(package_id.lower())
    missing = sorted(set(tracked) - set(listed_by))
    if missing:
        raise Unreadable("the listing has no entry for %s, which git tracks" % ", ".join(missing))
    for name, named in sorted(own.items()):
        lost = sorted(original for lower, original in named.items() if lower not in listed_by.get(name, set()))
        if lost:
            raise Unreadable("%s names %s and its listing does not have it" % (name, ", ".join(lost)))
    everywhere = set().union(*listed_by.values()) if listed_by else set()
    lost = sorted(original for lower, original in shared.items() if lower not in everywhere)
    if lost:
        raise Unreadable("a props or targets file names %s and the listing does not have it" % ", ".join(lost))
    if not found:
        raise Unreadable("the listing has no package at all")
    return found


class Hold:
    def __init__(self, entry, where):
        unknown = set(entry) - HOLD_KEYS
        if unknown:
            raise Malformed("%s has a key this gate does not know: %s" % (where, ", ".join(sorted(unknown))))
        self.package = field(entry, "package", str, where)
        if not PACKAGE_ID.match(self.package):
            raise Malformed("%s: %r is not a package id" % (where, self.package))
        self.through = Version(field(entry, "through", str, where))
        self.until = field(entry, "until", datetime.date, where)
        if isinstance(self.until, datetime.datetime):
            raise Malformed("%s: until is a date, without a time" % where)
        self.reason = one_line(field(entry, "reason", str, where))
        if len(self.reason) < 20:
            raise Malformed("%s: the reason has to say why, in a sentence" % where)


def holds(text):
    """The held versions, one per package, refused on anything it does not understand."""
    try:
        document = tomllib.loads(text)
    except tomllib.TOMLDecodeError as exc:
        raise Malformed("held.toml: %s" % exc) from exc
    unknown = set(document) - {"held"}
    if unknown:
        raise Malformed("held.toml has a table this gate does not know: %s" % ", ".join(sorted(unknown)))
    entries = document.get("held", [])
    if not isinstance(entries, list):
        raise Malformed("held.toml: held is not a list of tables")
    found = {}
    for number, entry in enumerate(entries):
        hold = Hold(entry, "held entry %d" % (number + 1))
        if hold.package.lower() in found:
            raise Malformed("held.toml holds %s twice" % hold.package)
        found[hold.package.lower()] = hold
    return found


# ---- the decision ---------------------------------------------------------------------------------


class Judgement:
    def __init__(self, package, version, state, target=None, note=""):
        self.package = package
        self.version = version
        self.state = state
        self.target = target
        self.note = note


def judge(package, version_text, releases, now, days, hold=None):
    """What one package in the tree is, against every version NuGet has of it."""
    version = Version(version_text)
    if not any(release.version == version for release in releases):
        raise Unreadable("%s %s is not among the %d versions NuGet lists for it - a page was not read"
                         % (package, version_text, len(releases)))
    newer = [r for r in releases if r.listed and r.version > version and (version.pre or not r.version.pre)]
    if not newer:
        return Judgement(package, version, "current")
    cutoff = now - datetime.timedelta(days=days)
    ripe = [r for r in newer if r.published <= cutoff]
    if not ripe:
        first = min(newer, key=lambda r: r.published)
        newest = max(newer, key=lambda r: r.version.key)
        return Judgement(package, version, "cooling", newest,
                         "past the cooldown on %s" % (first.published + datetime.timedelta(days=days)).date())
    target = max(ripe, key=lambda r: r.version.key)
    if hold is not None:
        if target.version <= hold.through and now.date() <= hold.until:
            return Judgement(package, version, "held", target,
                             "held through %s until %s: %s" % (hold.through, hold.until, hold.reason))
        if now.date() > hold.until:
            return Judgement(package, version, "behind", target, "the hold expired on %s" % hold.until)
        return Judgement(package, version, "behind", target, "newer than the hold through %s" % hold.through)
    return Judgement(package, version, "behind", target)


# ---- the commands -----------------------------------------------------------------------------------


def describe(judgement, projects=()):
    target = judgement.target
    line = "%-8s %s %s" % (judgement.state, judgement.package, judgement.version)
    if target is not None:
        line += " -> %s (%s, published %s)" % (target.version, step(judgement.version, target.version),
                                               target.published.date())
    if judgement.note:
        line += " - " + one_line(judgement.note)
    if projects:
        line += " [%s]" % ", ".join(sorted(projects))
    return line


def canary(fetch=fetch_json, now=None, days=None):
    """Refuse a run in which a package years behind does not come out behind."""
    now = now or datetime.datetime.now(datetime.timezone.utc)
    try:
        if days is None:
            days = cooldown_days(read_text(os.path.join(repository_root(), ".github", "dependabot.yml")))
        base = registration_base(fetch)
        missed = 0
        for package, version, expected in CANARIES:
            judgement = judge(package, version, releases_of(package, base, fetch), now, days)
            caught = (judgement.state == "behind"
                      and step(judgement.version, judgement.target.version) == expected)
            print("canary: %s - %s" % (describe(judgement), "caught" if caught else "NOT CAUGHT"))
            missed += 0 if caught else 1
    except (Malformed, Unreadable, OSError, subprocess.CalledProcessError) as exc:
        return refused(exc)
    if missed:
        print("A package years behind did not come out behind. Until it does, no verdict of this run")
        print("  means anything - a current tree and a registry that answered nothing read the same.")
        return 1
    return 0


def summary_table(judgements, found, hold_problems):
    lines = ["## NuGet packages against nuget.org", "",
             "| Package | In the tree | Newer version | Published | Step | State | Projects |",
             "|---|---|---|---|---|---|---|"]
    for judgement in judgements:
        target = judgement.target
        projects = sorted(found[(judgement.package, judgement.version.text)])
        note = one_line(judgement.note).replace("|", "/")
        lines.append("| %s | %s | %s | %s | %s | %s | %s |" % (
            judgement.package, judgement.version,
            target.version if target else "",
            target.published.date() if target else "",
            step(judgement.version, target.version) if target else "",
            judgement.state + (" - " + note if note else ""),
            ", ".join(projects) if len(projects) < 4 else "%d projects" % len(projects)))
    for problem in hold_problems:
        lines.append("")
        lines.append("**%s**" % problem)
    return "\n".join(lines) + "\n"


def verdict(listing_path, held_path, summary_path, fetch=fetch_json, now=None):
    now = now or datetime.datetime.now(datetime.timezone.utc)
    try:
        root = repository_root()
        listing = json.loads(read_text(listing_path))
        own = {}
        shared = {}
        for path in git_files(root, "*.csproj", "*.props", "*.targets"):
            named = references(read_text(os.path.join(root, path)), path)
            if path.endswith(".csproj"):
                own[path] = named
            else:
                shared.update(named)
        found = tree(listing, root, git_files(root, "*.csproj"), own, shared)
        days = cooldown_days(read_text(os.path.join(root, ".github", "dependabot.yml")))
        held = holds(read_text(held_path))
        base = registration_base(fetch)
        cache = {}
        judgements = []
        for package, version in sorted(found, key=lambda key: (key[0].lower(), Version(key[1]).key)):
            if package.lower() not in cache:
                cache[package.lower()] = releases_of(package, base, fetch)
            judgements.append(judge(package, version, cache[package.lower()], now, days,
                                    held.get(package.lower())))
    except (Malformed, Unreadable, OSError, json.JSONDecodeError, subprocess.CalledProcessError) as exc:
        return refused(exc)

    in_tree = {package.lower() for package, _ in found}
    hold_problems = []
    for lower, hold in sorted(held.items()):
        # A hold a newer version went past is not listed here - that package is already behind,
        # with the reason in its note. These two are the holds that cover nothing at all.
        states = {j.state for j in judgements if j.package.lower() == lower}
        if lower not in in_tree:
            hold_problems.append("held.toml holds %s, which no project uses - remove the entry" % hold.package)
        elif states <= {"current", "cooling"}:
            hold_problems.append("held.toml holds %s through %s and nothing is held by it any more - "
                                 "remove the entry" % (hold.package, hold.through))

    judgements.sort(key=lambda j: (STATE_ORDER[j.state], j.package.lower()))
    print("cooldown: %d days, read from .github/dependabot.yml" % days)
    for judgement in judgements:
        print(describe(judgement, found[(judgement.package, judgement.version.text)]))
    for problem in hold_problems:
        print("HOLD: " + problem)
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as stream:
            stream.write(summary_table(judgements, found, hold_problems))

    behind = [j for j in judgements if j.state == "behind"]
    print("%d package versions read, %d behind, %d held, %d cooling, %d current, %d hold problem(s)." % (
        len(judgements), len(behind), sum(j.state == "held" for j in judgements),
        sum(j.state == "cooling" for j in judgements), sum(j.state == "current" for j in judgements),
        len(hold_problems)))
    if behind or hold_problems:
        print("Move the package, or hold the version in .github/outdated/held.toml with a reason and a date.")
        return 1
    return 0


# ---- the self-test ------------------------------------------------------------------------------------


class Closing(io.BytesIO):
    """A response body that remembers being closed."""

    def __init__(self):
        super().__init__(b"")
        self.was_closed = False

    def close(self):
        self.was_closed = True
        super().close()


def selftest():
    """Every decision on cases whose answer is known, the red ones included. Reads nothing outside."""
    checks = []

    def same(name, expected, actual):
        checks.append((name, expected == actual, expected, actual))

    def raises(name, kind, action):
        try:
            action()
        except kind:
            checks.append((name, True, kind.__name__, kind.__name__))
            return
        except Exception as exc:  # the wrong refusal is a failed check, not a crash of the test
            checks.append((name, False, kind.__name__, type(exc).__name__))
            return
        checks.append((name, False, kind.__name__, "nothing raised"))

    ordered = ["1.0.0", "1.0.1", "1.1.0-alpha", "1.1.0-alpha.1", "1.1.0-alpha.beta", "1.1.0-beta",
               "1.1.0-beta.2", "1.1.0-beta.11", "1.1.0-rc.1", "1.1.0", "1.1.0.1", "2.0.0"]
    same("NuGet order, twelve versions", ordered,
         [v.text for v in sorted((Version(t) for t in reversed(ordered)), key=lambda v: v.key)])
    same("1.0 is 1.0.0.0", True, Version("1.0") == Version("1.0.0.0"))
    same("build metadata does not order", True, Version("2.0.0+abc") == Version("2.0.0"))
    same("labels compare without case", True, Version("1.0.0-RC.1") == Version("1.0.0-rc.1"))
    raises("a version with a space is refused", Malformed, lambda: Version("1.0 .0"))
    raises("a version that is not a string is refused", Malformed, lambda: Version(5))
    same("step major", "major", step(Version("12.0.3"), Version("13.0.1")))
    same("step patch", "patch", step(Version("4.0.1"), Version("4.0.2")))

    now = datetime.datetime(2026, 11, 2, 12, tzinfo=datetime.timezone.utc)

    def at(days_ago):
        return now - datetime.timedelta(days=days_ago)

    def rel(text, days_ago, listed=True):
        return Release(Version(text), listed, at(days_ago))

    base = [rel("1.0.0", 400), rel("1.1.0", 200)]
    same("on the newest", "current", judge("P", "1.1.0", base, now, 7).state)
    judged = judge("P", "1.0.0", base, now, 7)
    same("behind a ripe minor", ("behind", "1.1.0"), (judged.state, judged.target.version.text))
    same("younger than the cooldown", "cooling", judge("P", "1.1.0", base + [rel("1.2.0", 3)], now, 7).state)
    judged = judge("P", "1.0.0", base + [rel("1.2.0", 3)], now, 7)
    same("newest cooling, an older newer one ripe", ("behind", "1.1.0"),
         (judged.state, judged.target.version.text))
    same("exactly the cooldown is ripe", "behind", judge("P", "1.1.0", base + [rel("1.2.0", 7)], now, 7).state)
    same("a prerelease does not count for a stable tree", "current",
         judge("P", "1.1.0", base + [rel("2.0.0-rc.1", 30)], now, 7).state)
    same("a prerelease counts for a prerelease tree", "behind",
         judge("P", "2.0.0-beta.1", base + [rel("2.0.0-beta.1", 60), rel("2.0.0-rc.1", 30)], now, 7).state)
    same("an unlisted newer version does not count", "current",
         judge("P", "1.1.0", base + [rel("1.3.0", 30, listed=False)], now, 7).state)
    same("the tree on an unlisted version is still found", "behind",
         judge("P", "1.0.0", [rel("1.0.0", 400, listed=False), rel("1.1.0", 200)], now, 7).state)
    raises("a tree version NuGet does not list is refused", Unreadable,
           lambda: judge("P", "1.0.5", base, now, 7))

    hold_text = ('[[held]]\npackage = "P"\nthrough = "1.1.0"\nuntil = 2026-12-01\n'
                 'reason = "A reason long enough to say why."\n')
    hold = holds(hold_text)["p"]
    same("held through the target, before the date", "held", judge("P", "1.0.0", base, now, 7, hold).state)
    judged = judge("P", "1.0.0", base + [rel("1.2.0", 30)], now, 7, hold)
    same("a version past the hold is behind", ("behind", "1.2.0"), (judged.state, judged.target.version.text))
    late = datetime.datetime(2026, 12, 2, 12, tzinfo=datetime.timezone.utc)
    same("an expired hold is behind", "behind", judge("P", "1.0.0", base, late, 7, hold).state)
    same("the last day of a hold still holds", "held",
         judge("P", "1.0.0", base, datetime.datetime(2026, 12, 1, 23, tzinfo=datetime.timezone.utc), 7, hold).state)
    raises("a hold key with a typo is refused", Malformed, lambda: holds(hold_text.replace("until", "untill")))
    raises("a hold without a reason is refused", Malformed,
           lambda: holds('[[held]]\npackage = "P"\nthrough = "1.1.0"\nuntil = 2026-12-01\nreason = "x"\n'))
    raises("a hold with a string for a date is refused", Malformed,
           lambda: holds(hold_text.replace("2026-12-01", '"2026-12-01"')))
    raises("a hold with a time is refused", Malformed,
           lambda: holds(hold_text.replace("2026-12-01", "2026-12-01T10:00:00")))
    raises("a package held twice is refused", Malformed, lambda: holds(hold_text + hold_text))
    same("no holds at all", {}, holds("# nothing held\n"))
    spread = holds(hold_text.replace('"A reason long enough to say why."',
                                     '"""A reason long enough\nto say why, on two lines."""'))["p"]
    same("a reason over two lines reads as one", "A reason long enough to say why, on two lines.", spread.reason)
    row = summary_table([Judgement("P", Version("1.0.0"), "held", Release(Version("1.1.0"), True, at(200)),
                                   "held: one\nline | two")], {("P", "1.0.0"): {"a.csproj"}}, [])
    same("a note with a newline and a bar stays in one table row", 1,
         sum(1 for line in row.splitlines() if line.startswith("| P |")))
    same("the rest of that note is in the same row", True,
         any(line.startswith("| P |") and "held: one line / two" in line for line in row.splitlines()))

    yml = ("updates:\n  - package-ecosystem: github-actions\n    cooldown:\n      default-days: 3\n"
           "  - package-ecosystem: nuget\n    directories:\n      - \"/src/*\"\n    cooldown:\n"
           "      default-days: 7\n  - package-ecosystem: pip\n    cooldown:\n      default-days: 9\n")
    same("the nuget cooldown, among three entries", 7, cooldown_days(yml))
    raises("no nuget entry is refused", Malformed, lambda: cooldown_days(yml.replace("nuget", "docker")))
    raises("a nuget entry without the days is refused", Malformed,
           lambda: cooldown_days(yml.replace("      default-days: 7\n", "")))

    same("references read across lines", {"a.b": "A.B", "c": "C"},
         references('<Project><ItemGroup><PackageReference Include="A.B" Version="1.0.0" />'
                    '<PackageReference\n    Include="C" Version="2.0.0"></PackageReference></ItemGroup></Project>',
                    "x.csproj"))
    same("a reference inside a comment is not one", {"a.b": "A.B"},
         references('<Project><ItemGroup><PackageReference Include="A.B" />'
                    '<!-- <PackageReference Include="Gone" /> --></ItemGroup></Project>', "x.csproj"))
    same("an old-style project in the MSBuild namespace", {"a.b": "A.B"},
         references('<?xml version="1.0" encoding="utf-8"?>'
                    '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">'
                    '<ItemGroup><PackageReference Include="A.B" /></ItemGroup></Project>', "x.csproj"))
    raises("a document type declaration is refused", Malformed,
           lambda: references('<!DOCTYPE p [<!ENTITY a "aaaa">]><Project>&a;</Project>', "x.csproj"))
    raises("a file that is not XML is refused", Malformed,
           lambda: references('<Project><PackageReference Include="A"></Project>', "x.csproj"))

    root = os.path.abspath(os.sep + "repo")

    def project(name, *packages):
        return {"path": os.path.join(root, name),
                "frameworks": [{"framework": "net10.0", "topLevelPackages": [
                    {"id": p, "requestedVersion": v, "resolvedVersion": v} for p, v in packages]}]}

    listing = {"version": 1, "projects": [project("a/a.csproj", ("A.B", "1.0.0")),
                                          project("c/c.csproj", ("C", "2.0.0"), ("A.B", "1.0.0"))]}
    tracked = ["a/a.csproj", "c/c.csproj"]
    own = {"a/a.csproj": {"a.b": "A.B"}, "c/c.csproj": {"c": "C", "a.b": "A.B"}}
    same("the tree, by package and version", {("A.B", "1.0.0"): {"a/a.csproj", "c/c.csproj"},
                                              ("C", "2.0.0"): {"c/c.csproj"}},
         tree(listing, root, tracked, own, {}))
    raises("a tracked project missing from the listing is refused", Unreadable,
           lambda: tree(listing, root, tracked + ["d/d.csproj"], own, {}))
    raises("a package one project lost is refused while another lists it", Unreadable,
           lambda: tree({"version": 1, "projects": [project("a/a.csproj", ("A.B", "1.0.0")),
                                                    project("c/c.csproj", ("C", "2.0.0"))]},
                        root, tracked, own, {}))
    raises("a package a props file names, missing everywhere, is refused", Unreadable,
           lambda: tree(listing, root, tracked, own, {"e": "E"}))
    same("a package a props file names, listed by one project, is enough", 2,
         len(tree(listing, root, tracked, own, {"c": "C"})))
    raises("a listing reporting a problem is refused", Unreadable,
           lambda: tree(dict(listing, problems=[{"level": "error", "text": "no assets"}]), root, [], {}, {}))
    raises("a listing of another format version is refused", Malformed,
           lambda: tree(dict(listing, version=2), root, [], {}, {}))
    raises("a listing whose version is true is refused", Malformed,
           lambda: tree(dict(listing, version=True), root, [], {}, {}))
    raises("a package without a resolved version is refused", Malformed,
           lambda: tree({"version": 1, "projects": [{"path": os.path.join(root, "x.csproj"), "frameworks": [
               {"framework": "net10.0", "topLevelPackages": [{"id": "A.B"}]}]}]}, root, [], {}, {}))

    host = "https://" + NUGET_HOST
    pages = {
        SERVICE_INDEX: {"resources": [{"@type": "Other", "@id": "x"},
                                      {"@type": REGISTRATION_TYPE, "@id": host + "/r/"}]},
        host + "/r/p/index.json": {"count": 2, "items": [
            {"count": 1, "items": [{"catalogEntry": {"version": "1.0.0", "published": "2025-01-01T00:00:00+00:00"}}]},
            {"count": 1, "@id": host + "/r/p/page/2.json"}]},
        host + "/r/p/page/2.json": {"count": 1, "items": [
            {"catalogEntry": {"version": "1.1.0", "published": "2025-06-01T00:00:00+00:00", "listed": True}}]},
    }
    fetch = pages.__getitem__
    found = releases_of("P", registration_base(fetch), fetch)
    same("an inline page and a fetched one", ["1.0.0", "1.1.0"], [r.version.text for r in found])
    short = dict(pages)
    short[host + "/r/p/page/2.json"] = {"count": 1, "items": []}
    raises("a page with fewer versions than its count is refused", Unreadable,
           lambda: releases_of("P", host + "/r/", short.__getitem__))
    fewer = dict(pages)
    fewer[host + "/r/p/index.json"] = dict(pages[host + "/r/p/index.json"], count=3)
    raises("an index with fewer pages than its count is refused", Unreadable,
           lambda: releases_of("P", host + "/r/", fewer.__getitem__))
    boolean = dict(pages)
    boolean[host + "/r/p/index.json"] = dict(pages[host + "/r/p/index.json"], count=True)
    raises("a count that is true is refused", Malformed,
           lambda: releases_of("P", host + "/r/", boolean.__getitem__))
    elsewhere = dict(pages)
    elsewhere[host + "/r/p/index.json"] = {"count": 1, "items": [{"count": 1, "@id": "file:///etc/hosts"}]}
    raises("a page on file:// is not opened", Unreadable,
           lambda: releases_of("P", host + "/r/", elsewhere.__getitem__))
    raises("a package id with a slash is refused", Malformed, lambda: releases_of("a/../b", host + "/r/", fetch))
    raises("a service index without registrations is refused", Malformed,
           lambda: registration_base(lambda url: {"resources": []}))
    raises("a registration on another host is refused", Unreadable,
           lambda: registration_base(lambda url: {"resources": [{"@type": REGISTRATION_TYPE,
                                                                 "@id": "https://example.org/r/"}]}))
    same("NuGet over HTTPS is opened", host + "/v3/x.json", checked_url(host + "/v3/x.json"))
    for name, url in (("plain HTTP", "http://%s/v3/x.json" % NUGET_HOST),
                      ("another host", "https://example.org/v3/x.json"),
                      ("a host that only starts like NuGet's", "https://%s.example.org/x.json" % NUGET_HOST),
                      ("credentials", "https://user:secret@%s/x.json" % NUGET_HOST),
                      ("another port", "https://%s:8443/x.json" % NUGET_HOST),
                      ("a file", "file:///etc/hosts")):
        raises("%s is not opened" % name, Unreadable, lambda url=url: checked_url(url))
    raises("a redirect to another host is not followed", Unreadable,
           lambda: CheckedRedirects().redirect_request(None, None, 302, "Found", {}, "https://example.org/x"))

    # The errors are kept alive here on purpose. CPython closes an HTTPError's body by itself when
    # the last reference to it goes, at the end of the except block, so without these references
    # this check passes whether the gate closes it or not - measured, the mutation that removed
    # the close went unnoticed until they were added.
    for code, kind in ((404, "refused at once"), (503, "retried, then refused")):
        bodies = []
        errors = []

        def failing(request, timeout, code=code, bodies=bodies, errors=errors):
            body = Closing()
            bodies.append(body)
            errors.append(urllib.error.HTTPError(request.full_url, code, "no", {}, body))
            raise errors[-1]

        raises("HTTP %d is %s" % (code, kind), Unreadable,
               lambda: fetch_json(host + "/x.json", open_url=failing, sleep=lambda seconds: None))
        same("every HTTP %d answer is closed" % code, (ATTEMPTS if code == 503 else 1, True),
             (len(bodies), all(body.was_closed for body in bodies)))

    raises("an answer that unpacks past the ceiling is refused", Unreadable,
           lambda: decompress(gzip_of(b"0" * (MOST_BYTES + 2))))
    raises("an answer cut short is refused", Unreadable, lambda: decompress(gzip_of(b"{}" * 500)[:-12]))
    same("an answer under the ceiling unpacks", b"{}", decompress(gzip_of(b"{}")))

    with tempfile.TemporaryDirectory() as folder:
        big = os.path.join(folder, "big.json")
        with open(big, "wb") as stream:
            stream.write(b"0" * 11)
        raises("a file past the ceiling is refused", Unreadable, lambda: read_text(big, limit=10))
        same("a file under the ceiling is read", "0" * 11, read_text(big, limit=11))

    canary_pages = dict(pages)
    canary_pages[host + "/r/newtonsoft.json/index.json"] = {"count": 1, "items": [{"count": 2, "items": [
        {"catalogEntry": {"version": "12.0.3", "published": "2019-11-09T00:00:00+00:00"}},
        {"catalogEntry": {"version": "13.0.1", "published": "2021-03-22T00:00:00+00:00"}}]}]}
    same("the canary is caught on a registry that answers", 0,
         quiet(lambda: canary(canary_pages.__getitem__, now, 7)))
    current = dict(canary_pages)
    current[host + "/r/newtonsoft.json/index.json"] = {"count": 1, "items": [{"count": 1, "items": [
        {"catalogEntry": {"version": "12.0.3", "published": "2019-11-09T00:00:00+00:00"}}]}]}
    same("the canary on a registry with nothing newer is NOT caught", 1,
         quiet(lambda: canary(current.__getitem__, now, 7)))

    failed = [check for check in checks if not check[1]]
    for name, passed, expected, actual in checks:
        if not passed:
            print("FAILED %s: expected %r, got %r" % (name, expected, actual))
    print("self-test: %d checks, %s" % (len(checks), "every one as expected" if not failed
                                         else "%d NOT as expected" % len(failed)))
    return 1 if failed else 0


def gzip_of(data):
    compressor = zlib.compressobj(9, zlib.DEFLATED, 16 + zlib.MAX_WBITS)
    return compressor.compress(data) + compressor.flush()


def quiet(action):
    """Run a command of this script with its printing kept out of the self-test's own output."""
    saved = sys.stdout
    sys.stdout = open(os.devnull, "w", encoding="utf-8")
    try:
        return action()
    finally:
        sys.stdout.close()
        sys.stdout = saved


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("selftest", help="every decision on cases whose answer is known")
    commands.add_parser("canary", help="refuse a run in which a package years behind is not behind")
    judging = commands.add_parser("verdict", help="red when the tree is behind a version past the cooldown")
    judging.add_argument("listing", help="dotnet list package --format json, after a restore")
    judging.add_argument("--held", required=True, help="the held versions")
    judging.add_argument("--summary", default="", help="a Markdown file to append the table to")
    args = parser.parse_args(argv)
    # An exception nobody expected would end Python with 1, which reads as "found something".
    # Whatever this script did not foresee is a 2. The verdict comes first, in a sentence, and the
    # traceback after it, because in a CI log it is the only record of where the gate stopped.
    try:
        if args.command == "selftest":
            return selftest()
        if args.command == "canary":
            return canary()
        return verdict(args.listing, args.held, args.summary)
    except Exception:  # deliberately broad - see above
        import traceback
        print("REFUSED: the gate stopped on something it does not handle, so this run gives no verdict.")
        print("Where it stopped, for whoever fixes the gate:")
        traceback.print_exc(file=sys.stdout)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
