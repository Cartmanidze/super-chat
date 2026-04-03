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
      const message = await response.text();
      throw new Error(message || `HTTP ${response.status}`);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }
}
