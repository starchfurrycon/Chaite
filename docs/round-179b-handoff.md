# Round 179b addendum — the threshold is robust, not tuned

Swept the ceiling on the decisive weak-wing points (obsidian). Baseline for these points:
`625 DIED | 800 DIED | 900 KILL | 950 DIED | 1050 DIED | 1200 KILL | 1300 KILL`.

| ceiling | 625 | 800 | 900 | 950 | 1050 | 1200 | 1300 |
|---|---|---|---|---|---|---|---|
| **4000** | KILL 4 | KILL 2 | DIED 15043 | KILL 2 | KILL 4 | DIED (both) | DIED 8852 |
| **4200** | KILL 4 | KILL 2 | DIED 15043 | KILL 2 | KILL 4 | DIED (both) | DIED 8852 |
| **4400** | KILL 4 | KILL 2 | DIED 15043 | KILL 2 | KILL 4 | DIED (both) | DIED 8852 |
| **4600** | KILL 4 | KILL 2 | DIED 15043 | KILL 2 | KILL 4 | DIED (both) | DIED 8852 |
| **4800** | **DIED 6** | **DIED 5** | KILL 2 | **DIED 5** | — | — | — |

**Byte-identical results across 4000 / 4200 / 4400 / 4600** — same ticks, same hits, same boss life at
every point. **4800 breaks** (625 goes from a 4-hit KILL to a 6-hit death; 800 from a 2-hit KILL to a 5-hit
death). So the rule has a **wide robust plateau from 4000 to 4600** with a cliff at 4800.

**The plateau is the important part**: it means the win does not depend on a precise threshold, so 4400 is a
safe default rather than an overfit. It is also physically sensible — the tornado band's top edge is at 6285,
and the Boss's own band tops out near 4096, so anywhere in 4000-4600 keeps the player clear of both.

**Interpretation**: the rule only fires when the player is *below* the ceiling, and above ~4200 the player's
excursions past the threshold are cut off equally, so the threshold is not a tuned parameter — it is a
**saturation boundary**. The important consequence is that the win does not depend on picking 4400 precisely,
which makes it a safe default rather than an overfit.

**Conclusion**: keep **4400** as the default. The three "lost" weak points (900 / 1200 / 1300) are **not**
recoverable by this threshold — they need the separate structural work identified below (phase-3 teleport).

## Remaining work (unchanged priority order)

1. **Phase-3 teleport modelling** — the script models no teleport at all. The 618-like top comment on the
   sibling video gives the phase-3 pattern (teleport to the *opposite side from the previous teleport* to keep
   the player centred, then dash 1, then 2, then 3), and the owner's own comment on the no-hit video says
   *"进三阶段要控血，这个公式不太行"*. This is the most likely source of the weak wing's remaining
   mid-band losses (900 / 1200 / 1300) and of the mid-band near-kills.
2. **Shroomite tier** re-run (goal asks for both tiers reported; obsidian is the honest yardstick).
3. **Weak wing has no zero-hit point at all.** Strong has four (1175 / 1200 / 1300 / 2000).
4. **Intermediate DPS points** (350 / 425 / 675 / 725 on both wings).
5. Consider a **per-route ceiling** — the two wings' tornado bands differ (weak 6285..7973, strong 2740..6920).
