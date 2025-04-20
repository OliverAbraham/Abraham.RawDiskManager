namespace RawDiskManager
{
    public enum OperationalStatus
    {
        Unknown                 = 0,      // The operational status is unknown.
        Other                   = 1,      // A vendor-specific operational status is specified by setting the property OtherOperationalStatusDescription.
        OK                      = 2,      // The disk is responding to commands and is in a normal operational state.
        Degraded                = 3,      // The disk is responding to commands but is not operating in an optimal state.
        Stressed                = 4,      // The disk is functioning but requires attention. For example, the disk may be overloaded or overheating.
        PredictiveFailure       = 5,      // The disk is functioning, but a failure is expected in the near future.
        Error                   = 6,      // An error has occurred.
        NonRecoverableError     = 7,      // A non-recoverable error has occurred.
        Starting                = 8,      // The disk is starting up.
        Stopping                = 9,      // The disk is shutting down.
        Stopped                 = 10,     // The disk has been cleanly and orderly stopped or shut down.
        InService               = 11,     // The disk is being configured, managed, cleaned, or otherwise serviced.
        NoContact               = 12,     // The storage provider is aware of the disk but has never been able to establish communication with it.
        LostCommunication       = 13,     // The storage provider is aware of the disk and has successfully communicated with it in the past, but the disk is currently unreachable.
        Aborted                 = 14,     // Similar to "Stopped," except the disk was abruptly stopped and may require configuration or maintenance.
        Dormant                 = 15,     // The disk is reachable but inactive.
        SupportingEntityInError = 16,     // Another device or connection on which the disk depends may require attention.
        Completed               = 17,     // The disk has completed an operation. This status value should be combined with OK, Error, or Degraded depending on the result of the operation.
        Online                  = 0xD010, // In Windows-based storage subsystems, this indicates that the object is online.
        NotReady                = 0xD011, // In Windows-based storage subsystems, this indicates that the object is not ready.
        NoMedia                 = 0xD012, // In Windows-based storage subsystems, this indicates that the object has no media present.
        Offline                 = 0xD013, // In Windows-based storage subsystems, this indicates that the object is offline.
        Failed                  = 0xD014  // In Windows-based storage subsystems, this indicates that the object is in a failed state.
    }
}