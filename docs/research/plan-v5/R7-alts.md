# R7 Alts & multibox — summary
Works: atomic writes, no cross-quarantine, monotonic heartbeats, tested LiveClients, Forget respects live-elsewhere. Owner 2-client test still open.
F1 Owned collectibles live-only (RewardUnlockReader.cs:143,166-173,400); snapshot stores none -> alts show "?" in Moonlit/dashboard. All quest-reward collectibles are character-bound (correct model).
F2 Character list sorted newest capture (SnapshotService.cs:339) -> rows jump under multibox; Compare defaults to newest (CharactersPane 1531-1549), not persisted.
F3 Per-character settings (SpoilerShieldByCharacter, PayoffGatesNoticedByCharacter, PayoffWhyOpenByCharacter) in Dalamud config, last-save-wins -> repeated notices, reverted overrides (D11 promised merge, not done).
F4 Other clients' saves bump session.Version -> full recompute of viewed char (SessionState.cs:64).
F5 Newer-schema/Invalid snapshots only logged; tooltip still says "updates each save"; sidecars not watched, lag one save.
F6 Viewed char forgotten elsewhere -> ghost; Forget refusal silent; pid-only identity; "47 h ago" vs "1 d ago".
F7 No hide/don't-track, no bulk prune, no DC grouping/search, no streamer masking, no multi-char matrix.
Proposals: A store owned collectibles in snapshot (additive) (Must,M); B stable char order + sticky Compare (Must,S); C "Who has it" matrix (Should,M); D per-character settings in shared merged user/characters.json (Should,M); E hide/don't track/bulk forget (Should,S-M); F narrower invalidation (Should,S-M); G honest multibox status (Could,S); H DC grouping + list search (Could,S).
Qs: additive snapshot field ok?; hidden chars scope; default order; multi-PC shared folder in scope?; flag stale-by-patch snapshots?; move per-char settings.
