/**
 * Структурированная ошибка от API. Если в response.body лежит ASP.NET ProblemDetails
 * (тип application/problem+json), мы извлекаем title/detail/status и кидаем уже
 * понятную ошибку — UI рендерит .message без сырых JSON-простыней.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly title?: string;
  readonly detail?: string;
  readonly traceId?: string;

  constructor(message: string, init: { status: number; title?: string; detail?: string; traceId?: string }) {
    super(message);
    this.name = "ApiError";
    this.status = init.status;
    this.title = init.title;
    this.detail = init.detail;
    this.traceId = init.traceId;
  }
}

type ProblemDetailsLike = {
  title?: string;
  detail?: string;
  status?: number;
  traceId?: string;
  errors?: Record<string, string[] | string>;
};

export class HttpApi {
  private readonly baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  async get<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(this.toUrl(path), {
      credentials: "include",
      ...init,
      method: "GET",
      headers: {
        Accept: "application/json",
        ...init?.headers,
      },
    });

    return this.readJson<T>(response);
  }

  async post<T>(path: string, body?: unknown, init?: RequestInit): Promise<T> {
    const response = await fetch(this.toUrl(path), {
      credentials: "include",
      ...init,
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...init?.headers,
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });

    return this.readJson<T>(response);
  }

  async delete<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(this.toUrl(path), {
      credentials: "include",
      ...init,
      method: "DELETE",
      headers: {
        Accept: "application/json",
        ...init?.headers,
      },
    });

    return this.readJson<T>(response);
  }

  private toUrl(path: string) {
    if (path.startsWith("http://") || path.startsWith("https://")) {
      return path;
    }

    return `${this.baseUrl}${path.startsWith("/") ? path : `/${path}`}`;
  }

  private async readJson<T>(response: Response): Promise<T> {
    if (!response.ok) {
      const raw = await response.text();
      throw this.parseApiError(response.status, raw);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  private parseApiError(status: number, raw: string): ApiError {
    if (!raw) {
      return new ApiError(`HTTP ${status}`, { status });
    }

    let parsed: ProblemDetailsLike | null = null;
    try {
      parsed = JSON.parse(raw) as ProblemDetailsLike;
    } catch {
      // Не JSON — отдаём текст как сообщение (бывает на nginx-уровневых 502/504).
      return new ApiError(raw, { status });
    }

    if (parsed && typeof parsed === "object") {
      const detail = parsed.detail?.trim();
      const title = parsed.title?.trim();
      const validationErrors = this.extractValidationErrors(parsed.errors);

      const message = validationErrors ?? detail ?? title ?? `HTTP ${status}`;
      return new ApiError(message, {
        status: parsed.status ?? status,
        title,
        detail,
        traceId: parsed.traceId,
      });
    }

    return new ApiError(`HTTP ${status}`, { status });
  }

  private extractValidationErrors(errors: ProblemDetailsLike["errors"]): string | undefined {
    if (!errors) return undefined;
    const messages: string[] = [];
    for (const value of Object.values(errors)) {
      if (Array.isArray(value)) {
        messages.push(...value);
      } else if (typeof value === "string") {
        messages.push(value);
      }
    }
    return messages.length > 0 ? messages.join(" ") : undefined;
  }
}
