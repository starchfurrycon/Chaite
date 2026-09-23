using System;
using System.Collections.Generic;
using System.Globalization;

namespace Chaite.Core
{
    /// <summary>One tick of control the enumerator may choose. The alphabet is
    /// deliberately small and fixed: three horizontal directions, jump on or
    /// off, dash on or off. Twelve actions per tick is the branching factor the
    /// closed-loop method has to beat, and it is the reason this file exists
    /// rather than a nested loop.</summary>
    public struct RouteAction
    {
        public int Direction;
        public bool Jump;
        public bool Dash;

        public PlayerControlFrame ToControls()
        {
            return new PlayerControlFrame
            {
                Left = Direction < 0,
                Right = Direction > 0,
                Jump = Jump,
                Dash = Dash,
            };
        }
    }

    /// <summary>The world the enumerator walks through. An interface rather than
    /// a concrete call so the search itself is testable without an engine, a
    /// trace or a boss: the pruning and completeness claims are about this
    /// file, and they must be checkable on a world small enough to brute force
    /// and compare against.</summary>
    public interface IRouteWorld
    {
        /// <summary>Advances one tick. Returns false when the forward model
        /// refuses the regime, which the enumerator treats as a dead end rather
        /// than a predicted position.</summary>
        bool TryStep(in PlayerMotionFrame state, in PlayerControlFrame controls,
            out PlayerMotionFrame next, out bool hit,
            out ForwardModelRefusal refusal);

        /// <summary>True when this state closes the loop: it is back in the band
        /// and bearing sector the loop started in.</summary>
        bool IsGoal(in PlayerMotionFrame state);

        /// <summary>Discretises a state for dominance. Two partial routes that
        /// land in the same bucket are treated as interchangeable.</summary>
        int Bucket(in PlayerMotionFrame state);

        /// <summary>Estimated ticks or distance remaining to close the loop.
        /// Only orders the open set; it never decides whether a route is
        /// accepted.</summary>
        float DistanceToGoal(in PlayerMotionFrame state);
    }

    public sealed class RouteSearchRequest
    {
        /// <summary>Hard ceiling on ticks in a route.</summary>
        public int StepBudget = 240;

        /// <summary>When set, a goal state is only accepted if the route reached
        /// it without taking a hit.
        ///
        /// Without this the search returns the first goal state it pops, and
        /// when the goal is "the end of the loop in any state" that can be a
        /// route that survives by being hit repeatedly: one measured loop
        /// returned twenty-seven hits from a search that had expanded two
        /// hundred seventy states, having stopped the moment it arrived. The
        /// route it was looking for is the hit-free one, so arrival alone must
        /// not end the search.</summary>
        public bool RequireCleanGoal;
        /// <summary>How many clean goal states to hand back. One is enough when
        /// the goal is a place, because arriving there is the whole answer. It
        /// is not enough when the goal is the end of a loop in any state: the
        /// state a route leaves behind is then what the next loop starts from,
        /// and a single one can be a dead end even though the loop had many
        /// clean routes through it. Measured: a composition that carried one
        /// state per boundary chained twenty-three loops and then stopped at a
        /// loop whose space it exhausted in six hundred fifty-five expansions,
        /// because from that one state no clean route existed.</summary>
        public int CollectGoals = 1;
        /// <summary>Edge length, in pixels, of the cell a goal state is assigned
        /// to when deciding whether it is a new one. Zero keeps every collected
        /// goal, which is what the search used to do.
        ///
        /// Collecting by pop order is the same as collecting in heuristic order,
        /// and a heuristic clusters: forty-eight goals taken that way turned out
        /// to be near-duplicates of each other, and replaying all forty-eight of
        /// them through the engine produced three distinct runs. One goal per
        /// cell spreads the same budget over the states the loop can actually
        /// leave behind, which is what the next loop starts from.</summary>
        public float GoalCellSize;
        /// <summary>How many goals may be skipped for landing in an occupied
        /// cell before the search gives up spreading and returns what it has.
        /// Without a bound a world whose goals all fall in one cell would search
        /// until the expansion ceiling for nothing.</summary>
        public int GoalSpreadSkips = 4096;
        /// <summary>How many partial routes survive each tick. The bound that
        /// turns 12^N into something a run can finish.</summary>
        public int BeamWidth = 64;
        /// <summary>Ceiling on expansions, so a pathological world cannot spend
        /// the whole budget on one search.</summary>
        public int MaxExpansions = 200000;
        /// <summary>Ordering weight on progress toward the goal. Higher expands
        /// states closer to closing first.</summary>
        public float HeuristicWeight = 1f;
    }

    public sealed class RouteSearchReport
    {
        /// <summary>A goal was reached. It may still carry hits: see
        /// <see cref="Clean"/>.</summary>
        public bool Found;
        /// <summary>The reached goal has zero hits, which is the acceptance
        /// criterion. Because the open set is ordered by hit count first, the
        /// first goal popped has the fewest hits of any goal within the bounds,
        /// so a goal that is not clean is evidence that no clean route exists
        /// there rather than that the search stopped early.</summary>
        public bool Clean;
        public int Ticks;
        public int Hits;
        public int Expanded;
        public int Generated;
        public int PrunedByDominance;
        public int PrunedByBeam;
        public int Refused;
        public int Truncated;
        public readonly Dictionary<string, int> RefusalsByReason =
            new Dictionary<string, int>();
        public readonly List<RouteAction> Route = new List<RouteAction>();
        /// <summary>The states the collected clean goals ended in, in the order
        /// they were accepted. The first entry is the state <see cref="Route"/>
        /// leads to.</summary>
        public readonly List<PlayerMotionFrame> GoalStates =
            new List<PlayerMotionFrame>();
        /// <summary>The route that leads to each entry of
        /// <see cref="GoalStates"/>, in the same order.</summary>
        public readonly List<List<RouteAction>> GoalRoutes =
            new List<List<RouteAction>>();
    }

    /// <summary>
    /// Enumerates routes inside one closed loop and returns the shortest
    /// zero-hit one that returns to the loop's start bucket.
    ///
    /// The method is exhaustive in intent and bounded in practice, and the two
    /// are reconciled by making the bounds explicit and reported rather than
    /// implicit. Expansion order is hits first, then the heuristic, then depth,
    /// which gives one property worth relying on: no route with a hit is ever
    /// expanded while a zero-hit frontier remains, so the first goal reached is
    /// a zero-hit one if a zero-hit one exists within the bounds.
    ///
    /// Two bounds give up completeness and both are counted in the report:
    /// dominance collapsing, which treats two states in the same bucket as
    /// interchangeable even though their exact positions differ, and the beam,
    /// which drops the worst of the frontier. When a search comes back empty the
    /// report therefore distinguishes "the space was exhausted" from "a bound
    /// was hit", which is the difference between "no such route" and "not
    /// searched yet". Neither bound is allowed to be silent.
    /// </summary>
    public static class RouteEnumerator
    {
        /// <summary>The twelve actions, in a fixed order so two runs over the
        /// same world generate the same nodes in the same sequence.</summary>
        public static readonly RouteAction[] Alphabet = BuildAlphabet();

        private static RouteAction[] BuildAlphabet()
        {
            var actions = new RouteAction[12];
            var at = 0;
            for (var direction = -1; direction <= 1; direction++)
                for (var jump = 0; jump < 2; jump++)
                    for (var dash = 0; dash < 2; dash++)
                        actions[at++] = new RouteAction
                        {
                            Direction = direction,
                            Jump = jump != 0,
                            Dash = dash != 0,
                        };
            return actions;
        }

        private sealed class Node
        {
            public PlayerMotionFrame State;
            public Node Parent;
            public RouteAction Action;
            public int Depth;
            public int Hits;
            public float Heuristic;
            public bool HasAction;
        }

        /// <summary>Best-first over (hits, heuristic, depth). Hits first because
        /// a route with a hit is never better than one without, however close it
        /// got; the heuristic only orders within an equal hit count, so it can
        /// never talk the search out of a clean route.</summary>
        private sealed class NodeOrder : IComparer<Node>
        {
            public int Compare(Node left, Node right)
            {
                if (left.Hits != right.Hits) return left.Hits.CompareTo(right.Hits);
                var heuristic = left.Heuristic.CompareTo(right.Heuristic);
                if (heuristic != 0) return heuristic;
                return left.Depth.CompareTo(right.Depth);
            }
        }

        public static RouteSearchReport Search(IRouteWorld world,
            in PlayerMotionFrame start, RouteSearchRequest request)
        {
            if (world == null) throw new ArgumentNullException("world");
            if (request == null) throw new ArgumentNullException("request");
            var report = new RouteSearchReport();

            var startNode = new Node { State = start, Depth = 0, Hits = 0 };
            // Cells already represented among the collected goals, and how many
            // arrivals have been passed over for landing in one of them.
            var goalCells = request.GoalCellSize > 0f
                ? new HashSet<string>(StringComparer.Ordinal)
                : null;
            var goalSkips = 0;
            // The start bucket is the goal bucket: closing the loop means
            // returning to where the loop began.
            var open = new PriorityQueue();
            open.Push(startNode, new NodeOrder());

            // bucket -> fewest hits any route has reached it with. A partial
            // route arriving later with more hits can never overtake one that
            // arrived with fewer, because hits only accumulate. This is the
            // approximation: equal buckets are not equal states, so a route
            // pruned here might in principle have had a future the surviving one
            // does not. The bucket has to be fine enough that this is rare, and
            // PrunedByDominance in the report is how often it fired.
            //
            // The start bucket is deliberately NOT seeded. Seeding it would mark
            // the start as already reached with zero hits, and since closing the
            // loop means returning to exactly that bucket, every candidate that
            // closed the loop would be pruned as dominated by the start itself.
            // That is not a subtle bias; it is the goal being deleted.
            var bestHits = new Dictionary<int, int>();

            while (open.Count > 0)
            {
                if (report.Expanded >= request.MaxExpansions)
                {
                    report.Truncated = 1;
                    break;
                }
                var node = open.Pop();
                if (node.HasAction && world.IsGoal(in node.State) &&
                    (!request.RequireCleanGoal || node.Hits == 0))
                {
                    report.Found = true;
                    report.Clean = node.Hits == 0;
                    report.Ticks = node.Depth;
                    report.Hits = node.Hits;
                    // The route list is shared across goals, so it has to be
                    // emptied first. Appending to it left the second goal's
                    // route holding the first goal's actions as well, and
                    // replaying that ran past the end of the loop and was
                    // refused: one measured composition refused a hundred forty-
                    // nine replays and carried a single state per boundary
                    // because of exactly this.
                    report.Route.Clear();
                    for (var walk = node; walk != null && walk.HasAction;
                        walk = walk.Parent)
                        report.Route.Add(walk.Action);
                    report.Route.Reverse();
                    if (goalCells != null)
                    {
                        // Spread the budget over the cells the loop can leave the
                        // player in. A goal that lands in a cell already
                        // represented is passed over rather than counted, so the
                        // search keeps going until the cells are covered or the
                        // skip bound says the world only has one cell to offer.
                        // Velocity gets a quarter of the position cell, so the
                        // cells here are at least as fine as the ones the
                        // composition deduplicates on and nothing collected is
                        // thrown away downstream for landing in a taken cell.
                        var cell = string.Format(CultureInfo.InvariantCulture,
                            "{0:F0}|{1:F0}|{2:F0}|{3:F0}",
                            node.State.Position.X / request.GoalCellSize,
                            node.State.Position.Y / request.GoalCellSize,
                            node.State.Velocity.X / (request.GoalCellSize / 4f),
                            node.State.Velocity.Y / (request.GoalCellSize / 4f));
                        if (!goalCells.Add(cell))
                        {
                            goalSkips++;
                            if (goalSkips > request.GoalSpreadSkips)
                            {
                                report.Truncated = 1;
                                return report;
                            }
                            continue;
                        }
                    }
                    report.GoalStates.Add(node.State);
                    report.GoalRoutes.Add(new List<RouteAction>(report.Route));
                    report.Expanded++;
                    if (report.GoalStates.Count >= Math.Max(1, request.CollectGoals))
                        return report;
                    // More goals are wanted, so this arrival is recorded and the
                    // search continues. The node is not expanded further: it is
                    // already at the end of the loop, and its successors belong
                    // to the next loop's problem.
                    continue;
                }
                if (node.Depth >= request.StepBudget)
                {
                    report.Truncated = 1;
                    continue;
                }
                report.Expanded++;

                foreach (var action in Alphabet)
                {
                    report.Generated++;
                    PlayerMotionFrame next;
                    bool hit;
                    ForwardModelRefusal refusal;
                    if (!world.TryStep(in node.State, action.ToControls(),
                            out next, out hit, out refusal))
                    {
                        report.Refused++;
                        var name = refusal.ToString();
                        report.RefusalsByReason[name] =
                            report.RefusalsByReason.ContainsKey(name)
                                ? report.RefusalsByReason[name] + 1 : 1;
                        continue;
                    }
                    var hits = node.Hits + (hit ? 1 : 0);
                    var bucket = world.Bucket(in next);
                    int known;
                    if (bestHits.TryGetValue(bucket, out known) && known <= hits)
                    {
                        report.PrunedByDominance++;
                        continue;
                    }
                    bestHits[bucket] = hits;
                    var child = new Node
                    {
                        State = next,
                        Parent = node,
                        Action = action,
                        Depth = node.Depth + 1,
                        Hits = hits,
                        Heuristic = request.HeuristicWeight *
                            world.DistanceToGoal(in next),
                        HasAction = true,
                    };
                    open.Push(child, new NodeOrder());
                }

                // The beam is applied to the frontier, not to the children, so
                // it bounds memory without changing which child is generated
                // first from a given parent.
                if (open.Count > request.BeamWidth)
                {
                    var dropped = open.Count - request.BeamWidth;
                    report.PrunedByBeam += dropped;
                    open.Trim(request.BeamWidth);
                }
            }
            return report;
        }

        /// <summary>What a full sweep of one loop's reachable states produced.
        ///
        /// The search above is a best-first walk with a beam and a bucketed
        /// dominance rule, and both of those are approximations: the bucket can
        /// delete the only route that closes the loop, and the beam can delete it
        /// before the bucket ever sees it. This reports the alternative the user
        /// asked for -- sweep every reachable state, deduplicate exactly, prune
        /// nothing -- together with the frontier size at each depth, because
        /// whether the sweep is affordable is the whole question and it should be
        /// answered by measurement rather than assumed.
        /// </summary>
        public sealed class ExhaustiveReport
        {
            /// <summary>Frontier size after each tick. The last entry is the
            /// largest number of distinct states the loop can be in at once.</summary>
            public readonly List<int> Frontier = new List<int>();

            /// <summary>Distinct states reached, summed over all depths.</summary>
            public int States;

            /// <summary>Expansions performed, which is States times the alphabet
            /// size minus the states that were not expanded.</summary>
            public long Expansions;

            /// <summary>Steps the forward model refused, which is the mobility
            /// constraint doing the pruning.</summary>
            public long Refused;

            /// <summary>Why the refusals happened. A sweep that refuses every
            /// action at the root is not measuring mobility; it is measuring a
            /// world that will not start, and the reason says which.</summary>
            public readonly Dictionary<string, long> RefusalsByReason =
                new Dictionary<string, long>(StringComparer.Ordinal);

            /// <summary>Goal arrivals: states that close the loop, clean.</summary>
            public readonly List<PlayerMotionFrame> GoalStates =
                new List<PlayerMotionFrame>();

            public readonly List<List<RouteAction>> GoalRoutes =
                new List<List<RouteAction>>();

            /// <summary>True when the sweep hit the state ceiling rather than
            /// running out of reachable states. Reported, never hidden.</summary>
            public bool Truncated;

            /// <summary>States discarded by the per-depth score cut. Reported so
            /// that a sweep which is really a beam says so.</summary>
            public long Dropped;

            /// <summary>The largest frontier seen, which is the number that says
            /// whether the loop is exhaustible.</summary>
            public int Widest
            {
                get
                {
                    var widest = 0;
                    foreach (var size in Frontier)
                        if (size > widest) widest = size;
                    return widest;
                }
            }
        }

        /// <summary>Sweeps every state reachable within the loop's tick budget,
        /// keeping only routes that have taken no hit and deduplicating states
        /// exactly rather than by bucket.
        ///
        /// Hits are dropped rather than carried. The goal is a hit-free route
        /// that closes the loop, so a partial route that has already been hit can
        /// never become one, and dropping it is not a heuristic -- it is the
        /// objective applied early.
        ///
        /// The mobility constraint is the forward model's own refusal, so the
        /// tree is bounded by what the character can actually do rather than by a
        /// number chosen in advance.
        /// </summary>
        public static ExhaustiveReport Exhaustive(IRouteWorld world,
            in PlayerMotionFrame start, int stepBudget, int stateCeiling)
        {
            return Exhaustive(world, in start, stepBudget, stateCeiling, 0, null);
        }

        /// <summary>As above, but keeps only the best few states per depth.
        ///
        /// The sweep is exponential: measured on a real loop the frontier grows by
        /// about four point six per tick, so the tenth tick already holds one and
        /// seven tenths of a million distinct states and a thirty tick loop is
        /// four point six to the thirtieth. The forward model refuses nothing --
        /// every action is legal from every state -- so the mobility constraint
        /// prunes exactly zero and only deduplication bends the curve.
        ///
        /// Something therefore has to be dropped, and what is dropped matters. A
        /// bucket that merges states is the wrong instrument, because it can
        /// delete the one route that closes the loop and that is the failure this
        /// project has already been bitten by twice. Keeping the best N by a score
        /// drops whole states rather than merging them, and the score is supplied
        /// by the caller along with its weights so that the choice of what to
        /// prefer is not made here. This class enumerates; it does not decide what
        /// a good route looks like.
        /// </summary>
        public static ExhaustiveReport Exhaustive(IRouteWorld world,
            in PlayerMotionFrame start, int stepBudget, int stateCeiling,
            int keepPerDepth, Func<PlayerMotionFrame, float> score)
        {
            if (world == null) throw new ArgumentNullException("world");
            var report = new ExhaustiveReport();
            if (stepBudget <= 0) return report;
            if (keepPerDepth < 0) keepPerDepth = 0;

            var seen = new HashSet<StateKey>();
            var frontier = new List<Node>();
            var root = new Node { State = start, Depth = 0, Hits = 0 };
            seen.Add(StateKey.From(in start));
            frontier.Add(root);

            for (var depth = 0; depth < stepBudget && frontier.Count > 0; depth++)
            {
                var next = new List<Node>();
                foreach (var node in frontier)
                {
                    foreach (var action in Alphabet)
                    {
                        report.Expansions++;
                        PlayerMotionFrame stepped;
                        bool hit;
                        ForwardModelRefusal refusal;
                        if (!world.TryStep(in node.State, action.ToControls(),
                                out stepped, out hit, out refusal))
                        {
                            report.Refused++;
                            var reason = refusal.ToString();
                            long known;
                            report.RefusalsByReason.TryGetValue(reason, out known);
                            report.RefusalsByReason[reason] = known + 1;
                            continue;
                        }
                        // Already hit: it can never be a hit-free route, so it is
                        // not carried. This is the objective, not a bound.
                        if (hit) continue;
                        if (!seen.Add(StateKey.From(in stepped))) continue;
                        var child = new Node
                        {
                            State = stepped,
                            Parent = node,
                            Action = action,
                            Depth = node.Depth + 1,
                            Hits = 0,
                            HasAction = true,
                        };
                        if (score != null)
                            child.Heuristic = score(stepped);
                        next.Add(child);
                    }
                    // Checked inside the depth, not only after it. A single depth
                    // can be the one that blows up, and a ceiling tested once the
                    // whole layer is already in memory does not bound anything.
                    if (seen.Count > stateCeiling)
                    {
                        report.States += next.Count;
                        report.Frontier.Add(next.Count);
                        report.Truncated = true;
                        return report;
                    }
                }
                if (keepPerDepth > 0 && next.Count > keepPerDepth)
                {
                    // Whole states are dropped, not merged. The survivors are the
                    // highest scoring, so what the caller's weights prefer is what
                    // continues; nothing is averaged and no two routes are
                    // declared interchangeable.
                    next.Sort(ScoreOrder);
                    report.Dropped += next.Count - keepPerDepth;
                    next.RemoveRange(keepPerDepth, next.Count - keepPerDepth);
                }
                report.States += next.Count;
                report.Frontier.Add(next.Count);
                foreach (var node in next)
                {
                    if (!world.IsGoal(in node.State)) continue;
                    var route = new List<RouteAction>();
                    for (var walk = node; walk != null && walk.HasAction;
                        walk = walk.Parent)
                        route.Add(walk.Action);
                    route.Reverse();
                    report.GoalStates.Add(node.State);
                    report.GoalRoutes.Add(route);
                }
                frontier = next;
            }
            return report;
        }

        /// <summary>Highest score first, with the depth as a tie-break so the
        /// order is total and the sweep is reproducible.</summary>
        private static int ScoreOrder(Node left, Node right)
        {
            if (left.Heuristic > right.Heuristic) return -1;
            if (left.Heuristic < right.Heuristic) return 1;
            if (left.Depth < right.Depth) return -1;
            if (left.Depth > right.Depth) return 1;
            return 0;
        }

        /// <summary>Exact state identity for the sweep, packed rather than
        /// formatted.
        ///
        /// The first version built a string per state and the sweep exhausted
        /// memory inside the formatter before it could report a frontier, which
        /// left the real question -- how wide the loop gets -- unanswered while
        /// looking like an answer about the loop. Position and velocity are
        /// quantised to a hundredth of a pixel, which is finer than any tolerance
        /// this project has measured as meaningful, and the counters that change
        /// what the next tick does are included so that two states which merely
        /// look alike are not merged.
        /// </summary>
        private struct StateKey : IEquatable<StateKey>
        {
            private int _px, _py, _vx, _vy, _wing, _rocket, _dash, _grounded;

            public static StateKey From(in PlayerMotionFrame state)
            {
                var key = new StateKey();
                key._px = (int)Math.Round(state.Position.X * 100f);
                key._py = (int)Math.Round(state.Position.Y * 100f);
                key._vx = (int)Math.Round(state.Velocity.X * 100f);
                key._vy = (int)Math.Round(state.Velocity.Y * 100f);
                key._wing = (int)Math.Round(state.Flight.WingTime * 100f);
                key._rocket = (int)Math.Round(state.Flight.RocketTime * 100f);
                key._dash = state.Dash.DashDelay;
                key._grounded = state.Grounded ? 1 : 0;
                return key;
            }

            public bool Equals(StateKey other)
            {
                return _px == other._px && _py == other._py &&
                    _vx == other._vx && _vy == other._vy &&
                    _wing == other._wing && _rocket == other._rocket &&
                    _dash == other._dash && _grounded == other._grounded;
            }

            public override bool Equals(object other)
            {
                return other is StateKey && Equals((StateKey)other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + _px;
                    hash = hash * 31 + _py;
                    hash = hash * 31 + _vx;
                    hash = hash * 31 + _vy;
                    hash = hash * 31 + _wing;
                    hash = hash * 31 + _rocket;
                    hash = hash * 31 + _dash;
                    hash = hash * 31 + _grounded;
                    return hash;
                }
            }
        }

        /// <summary>Minimal binary heap. Written here rather than taken from the
        /// framework because the ordering is a comparer object and this keeps the
        /// allocation per push to one array slot.</summary>
        private sealed class PriorityQueue
        {
            private Node[] _items = new Node[64];
            private IComparer<Node> _order;
            public int Count { get; private set; }

            public void Push(Node node, IComparer<Node> order)
            {
                _order = order;
                if (Count == _items.Length)
                    Array.Resize(ref _items, _items.Length * 2);
                _items[Count] = node;
                var index = Count++;
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (_order.Compare(_items[parent], _items[index]) <= 0) break;
                    Swap(parent, index);
                    index = parent;
                }
            }

            public Node Pop()
            {
                var result = _items[0];
                _items[0] = _items[--Count];
                _items[Count] = null;
                var index = 0;
                while (true)
                {
                    var left = index * 2 + 1;
                    if (left >= Count) break;
                    var smallest = left;
                    var right = left + 1;
                    if (right < Count &&
                        _order.Compare(_items[right], _items[left]) < 0)
                        smallest = right;
                    if (_order.Compare(_items[index], _items[smallest]) <= 0) break;
                    Swap(index, smallest);
                    index = smallest;
                }
                return result;
            }

            /// <summary>Keeps the best <paramref name="keep"/> entries. Rebuilds
            /// rather than removing one at a time, because the beam drops many
            /// at once and a heapify is cheaper than that many pops.</summary>
            public void Trim(int keep)
            {
                var kept = new List<Node>(keep);
                for (var i = 0; i < keep; i++) kept.Add(Pop());
                Count = 0;
                foreach (var node in kept) Push(node, _order);
            }

            private void Swap(int left, int right)
            {
                var temporary = _items[left];
                _items[left] = _items[right];
                _items[right] = temporary;
            }
        }
    }
}
