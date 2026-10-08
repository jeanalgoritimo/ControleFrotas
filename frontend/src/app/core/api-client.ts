import { Injectable } from "@angular/core";
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}
@Injectable({ providedIn: "root" })
export class ApiClient {
  csrf = "";
  async request<T>(url: string, method = "GET", body?: unknown): Promise<T> {
    let response: Response;
    try {
      response = await fetch(`/api${url}`, {
        method,
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": this.csrf,
        },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    } catch {
      throw new ApiError(
        "Não foi possível conectar à API. Confira se o servidor está iniciado.",
        0,
      );
    }
    if (!response.ok) {
      const fallback =
        response.status === 429
          ? "Muitas tentativas. Aguarde um minuto."
          : response.status >= 500
            ? "Servidor indisponível. Confira o terminal da API."
            : "Não foi possível concluir a operação.";
      const data = await response.json().catch(() => ({ message: fallback }));
      throw new ApiError(
        data.message ?? data.detail ?? fallback,
        response.status,
      );
    }
    return response.status === 204
      ? (undefined as T)
      : (response.json() as Promise<T>);
  }
}
