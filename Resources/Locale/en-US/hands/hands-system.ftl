# Examine text after when they're holding something (in-hand)
comp-hands-examine = { CAPITALIZE(SUBJECT($user)) } { CONJUGATE-BE($user) } holding { $items }.
comp-hands-examine-empty = { CAPITALIZE(SUBJECT($user)) } { CONJUGATE-BE($user) } not holding anything.

# Floof
comp-hands-examine-wrapper = { PROPER($item) ->
    *[false] { INDEFINITE($item) } [color=paleturquoise]{$item}[/color]
    [true] [color=paleturquoise]{$item}[/color]
}

comp-hands-examine-cuffed-all = { CAPITALIZE(POSS-ADJ($user)) } hands are cuffed.
comp-hands-examine-cuffed-some = { CAPITALIZE(NUMBER-WORDS($number)) } of { POSS-ADJ($user) } hands are cuffed.

comp-hands-examine-drag = { CAPITALIZE(SUBJECT($user)) } { CONJUGATE-BE($user) } dragging { $item }.
# End Floof

hands-system-blocked-by = Blocked by
