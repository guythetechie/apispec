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

model ETag {
    eTag: NonEmptyString;
}

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
    @doc("Request is invalid.")
    ifMatchHeaderInvalid: {
        @statusCode _: 400,
        code: ErrorCode.InvalidConditionalHeader | ErrorCode.InvalidId,
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

union GetResponse<TResource extends object> {
    @doc("Resource ID is invalid.")
    resourceIdIsInvalid: {
        @statusCode _: 400,
        code: ErrorCode.InvalidId,
        message: NonEmptyString,
    },
    @doc("Resource ID was not found")
    eTagMismatch: {
        @statusCode _: 404,
        code: ErrorCode.ResourceNotFound,
        message: NonEmptyString,
    },
    @doc("Resource was found.")
    success: {
        @statusCode _: 200,
        ...TResource,
        ...ETag,
    },
}

@delete
@route("{id}")
op deleteResource<TId extends ResourceId>(
    @path id: TId,
    ...IfMatchHeader
): DeleteResponse;

@get
@route("{id}")
op getResource<TId extends ResourceId, TResource extends object>(
    @path id: TId
): GetResponse<TResource>;
