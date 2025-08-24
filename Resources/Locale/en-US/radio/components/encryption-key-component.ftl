encryption-key-successfully-installed = You put the encryption key inside.
encryption-key-slots-already-full = There is no place for another encryption key.
# Floof: locale strings for unremovable keys, pluralization handling
encryption-keys-all-extracted = { $count ->
    [one] You pop out the encryption key!
    *[other] You pop out the encryption keys!
}
encryption-keys-some-extracted = { $count ->
    [one] You pop out one of the encryption keys!
    *[other] You pop out some of the encryption keys!
}
encryption-keys-none-extracted = { $remaining ->
    [one] The encryption key is unremovable!
    *[other] The encryption keys are unremovable!
}
encryption-keys-no-keys = This device has no encryption keys!
encryption-keys-are-locked = Encryption key slots are locked!
encryption-keys-panel-locked = Open maintenance panel first!

examine-encryption-channels-prefix = Available frequencies:
examine-encryption-channel = [color={$color}]{$key} for {$id} ({$freq})[/color]
examine-encryption-default-channel = The default channel is [color={$color}]{$channel}[/color].
