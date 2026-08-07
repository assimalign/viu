# Assimalign.Viu.Testing

Testing provides a DOM-free host and queries refreshed `MountedComponentView<TestNode>` values.
It does not retain mounted engine nodes or downcast `ComponentContext`.

Views are engine-cached with stable per-mount reference identity, but host ranges and mount state
can change after each scheduler flush, so the harness reacquires views instead of holding engine
objects.
