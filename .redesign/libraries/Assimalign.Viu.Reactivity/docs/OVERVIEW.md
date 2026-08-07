# Assimalign.Viu.Reactivity

Reactivity remains the dependency-free implementation of dependency tracking, effects, scopes,
references, collections, and watches. This design scaffold includes only the contracts needed to
make package ownership visible: concrete effect-scope lifetime and the genuine watch scheduler port.

The production engine would move without semantic redesign. A compiler block remains a patch unit;
it does not become a reactive subscriber in this proposal.
