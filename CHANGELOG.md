# Changelog

All notable changes to Better Windows Services are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file is for people who use the tool. Changes that only matter to somebody working on
it - internal structure, measurements, test guards - are kept in a developer changelog that
is not part of this repository.

## [Unreleased]

### Changed

- The details panel reads the signature, publisher, file version, file hash, memory and
  "Required by" of its entry when it opens, instead of showing "not read" and asking you to
  turn on a column. While it reads, those lines say "reading...". A file on another machine is
  still not read, and the panel says why.
- The details panel takes a third of the window, between a lower and an upper limit, instead
  of a fixed width. While it is open, the Description column leaves the list, because the
  panel shows the description in full. The column comes back when the panel closes, and your
  choice of columns is kept.
- "What it runs" comes second in the details panel, before "About the entry".

### Fixed

- The details panel no longer stays open over the machine overview.
- Opening the details panel on another entry no longer shows the previous entry's "no longer
  in the listing" notice.

## [0.2.0] - 2026-09-24

First release. What the tool does is described in the [README](README.md).
