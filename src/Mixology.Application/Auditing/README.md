# Activity and audit recording

Audit is a protocol split between `Mixology.Application` and the Audit bounded
context. The application pipeline describes an `OperationActivity`; the module
implements `IActivityRecorder` and persists immutable `AuditEntry` rows.

`TrackActivityMiddleware` starts one activity for a command. Domain operations
set the primary resource and call the operation context's touch API for other
material entities. Event-handler touches join the originating activity because
their restricted context shares the same unit of work.

On success, `RecordSuccessfulActivityMiddleware` writes the activity before the
outer transaction commits. A later event failure rolls back both business state
and that success record. The failure path records a rejected attempt only after
rollback, preventing a failed transaction from erasing its own diagnostic
history.

Queries are not audited. Audit entries themselves are immutable and not
taggable. Presentation surfaces query them through the same authorization,
filtering, paging, and typed-error rules as other modules.
