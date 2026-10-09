import { Component, signal, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";

import { Vehicle, Driver, Audit } from "./app/features/fleet/fleet.models";
import { Login } from "./app/features/auth/login";
import { Overview } from "./app/features/overview/overview";
import { FleetWorkspace } from "./app/features/fleet/fleet-workspace";
import { AuditList } from "./app/features/audit/audit-list";
import { FuelWorkspace } from "./app/features/fuel/fuel-workspace";
import { ApiClient, ApiError } from "./app/core/api-client";
@Component({
  selector: "app-root",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    Login,
    Overview,
    AuditList,
    FleetWorkspace,
    FuelWorkspace,
  ],
  templateUrl: "./app.html",
})
export class App {
  fuelRefresh = signal(0);
  user = signal("");
  loading = signal(false);
  message = signal("");
  error = signal("");
  vehicles = signal<Vehicle[]>([]);
  drivers = signal<Driver[]>([]);
  audits = signal<Audit[]>([]);
  tab = signal("overview");
  menuCollapsed = signal(this.readMenuPreference());
  toggleMenu() {
    this.menuCollapsed.update((value) => !value);
    try {
      localStorage.setItem(
        "ControleFrotas.menuCollapsed",
        String(this.menuCollapsed()),
      );
    } catch {
      /* O menu continua funcionando quando o armazenamento está indisponível. */
    }
  }
  private readMenuPreference(): boolean {
    try {
      return localStorage.getItem("ControleFrotas.menuCollapsed") === "true";
    } catch {
      return false;
    }
  }
  api = inject(ApiClient);
  constructor() {
    void this.restore();
  }
  async request<T>(url: string, method = "GET", body?: unknown): Promise<T> {
    try {
      return await this.api.request<T>(url, method, body);
    } catch (e) {
      if (e instanceof ApiError && e.status === 401 && url !== "/auth/login")
        this.user.set("");
      throw e;
    }
  }
  async refreshCsrf() {
    this.api.csrf = (await this.request<{ token: string }>("/auth/csrf")).token;
  }
  async restore() {
    try {
      const me = await this.request<{ name: string }>("/auth/me");
      this.user.set(me.name);
      await this.refreshCsrf();
      await this.load();
    } catch {
      this.user.set("");
    }
  }
  async login(credentials: { username: string; password: string }) {
    this.loading.set(true);
    this.error.set("");
    try {
      await this.refreshCsrf();
      const me = await this.request<{ name: string }>(
        "/auth/login",
        "POST",
        credentials,
      );
      this.user.set(me.name);
      await this.refreshCsrf();
      await this.load();
    } catch (e) {
      this.error.set(this.describe(e));
    } finally {
      this.loading.set(false);
    }
  }
  async logout() {
    try {
      await this.request("/auth/logout", "POST");
      this.user.set("");
      this.vehicles.set([]);
      this.drivers.set([]);
      this.audits.set([]);
      this.api.csrf = "";
    } catch (e) {
      this.error.set(this.describe(e));
    }
  }
  async load() {
    this.loading.set(true);
    this.error.set("");
    try {
      const [vehicles, drivers, audits] = await Promise.all([
        this.request<Vehicle[]>("/vehicles"),
        this.request<Driver[]>("/drivers"),
        this.request<Audit[]>("/audit"),
      ]);
      this.vehicles.set(vehicles);
      this.drivers.set(drivers);
      this.audits.set(audits);
      this.fuelRefresh.update((value) => value + 1);
    } catch (e) {
      this.error.set(this.describe(e));
    } finally {
      this.loading.set(false);
    }
  }
  navigate(tab: string) {
    this.tab.set(tab);
    this.message.set("");
  }
  describe(e: unknown) {
    return e instanceof Error ? e.message : "Não foi possível conectar à API.";
  }
}
