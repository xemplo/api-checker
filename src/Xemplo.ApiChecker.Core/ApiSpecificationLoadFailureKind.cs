namespace Xemplo.ApiChecker.Core;

public enum ApiSpecificationLoadFailureKind
{
    InvalidSource,
    SourceNotFound,
    SourceReadFailed,
    SourceFetchFailed,
    ParseFailed,
    UnsupportedSpecificationVersion,
    ExternalReferencesNotSupported
}