using System.Runtime.CompilerServices;

// The PlayMode test assembly may see internals of the runtime assembly.
//
// Sanctioned in the Job 2 brief as the alternative to widening a god object's PUBLIC API for a
// test. L10 needs read-only views of BoatController's pathfinding result so it can drive the real
// FindValidMoves rather than keeping a second copy of the traversal rules - and a second copy is
// exactly the divergence this project keeps paying for (risk R1).
[assembly: InternalsVisibleTo("HapisHavoc.Tests.PlayMode")]
