public record ClientStatusUpdate(
    ConnectionStatus Connection,
    DateTime LastSuccess,
    DateTime LastFailure,
    int ConsecutiveFailures
);
