import "@typespec/http";

using TypeSpec.Http;

namespace ApiSpec.Common;

enum ErrorCode {
    ResourceNotFound,
    ResourceAlreadyExists,
    InvalidConditionalHeader,
    InvalidJsonBody,
    InvalidId,
    ETagMismatch,
    InternalServerError,
}

@error
model ApiError {
    code: ErrorCode;
    message: string;
    details?: ApiError[];
}

@minLength(1)
scalar NonEmptyString extends string;

scalar ETag extends NonEmptyString;

scalar ResourceId extends NonEmptyString;

@doc("If-Match header")
model IfMatchHeader {
    @header("If-Match")
    ifMatch: NonEmptyString;
}

union DeleteResponse {
    @doc("If-Match header is missing.")
    ifMatchHeaderMissing: {
        @statusCode _: 428,
        code: ErrorCode.InvalidConditionalHeader,
        message: NonEmptyString,
    },
    @doc("If-Match header is invalid.")
    ifMatchHeaderInvalid: {
        @statusCode _: 400,
        code: ErrorCode.InvalidConditionalHeader,
        message: NonEmptyString,
    },
    @doc("Resource ETag doesn't match If-Match header")
    eTagMismatch: {
        @statusCode _: 412,
        code: ErrorCode.ETagMismatch,
        message: NonEmptyString,
    },
    @doc("Resource was successfully deleted or no longer exists.")
    success: {
        @statusCode _: 204,
    },
}

@delete
@route("{id}")
op deleteResource<T extends ResourceId>(
    @path id: T,
    ...IfMatchHeader
): DeleteResponse;
