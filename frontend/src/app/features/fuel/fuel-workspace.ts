import {
  Component,
  ElementRef,
  ViewChild,
  input,
  output,
  signal,
  computed,
  inject,
  effect,
  untracked,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Vehicle } from "../fleet/fleet.models";
import { ApiClient } from "../../core/api-client";
import { FuelPage } from "./fuel.models";
@Component({
  selector: "fleet-fuel",
  imports: [CommonModule, FormsModule],
  templateUrl: "./fuel-workspace.html",
})
export class FuelWorkspace {
  @ViewChild("editor") editor!: ElementRef<HTMLDialogElement>;
  vehicles = input<Vehicle[]>([]);
  refreshKey = input(0);
  saved = output<string>();
  api = inject(ApiClient);
  loading = signal(false);
  saving = signal(false);
  error = signal("");
  modalError = signal("");
  data = signal<FuelPage>({
    items: [],
    totalCount: 0,
    totalLiters: 0,
    totalCost: 0,
  });
  activeVehicles = computed(() => this.vehicles().filter((v) => v.active));
  page = signal(1);
  pages = computed(() => Math.max(1, Math.ceil(this.data().totalCount / 20)));
  filterVehicle = 0;
  from = "";
  to = "";
  vehicleId = 0;
  date = this.today();
  odometer = 0;
  fuelType = "Gasolina";
  liters: number | null = null;
  unitPrice: number | null = null;
  station = "";
  reference = "";
  fullTank = false;
  requestId = "";
  vehicleVersion: string | null = null;
  private loadSequence = 0;
  constructor() {
    effect(() => {
      this.refreshKey();
      untracked(() => void this.load());
    });
  }
  today() {
    return new Intl.DateTimeFormat("sv-SE", {
      timeZone: "America/Sao_Paulo",
    }).format(new Date());
  }
  totalPreview() {
    return (this.liters ?? 0) * (this.unitPrice ?? 0);
  }
  async load() {
    const sequence = ++this.loadSequence;
    this.loading.set(true);
    this.error.set("");
    const query = new URLSearchParams({
      page: String(this.page()),
      vehicleId: String(this.filterVehicle),
    });
    if (this.from) query.set("from", this.from);
    if (this.to) query.set("to", this.to);
    try {
      const result = await this.api.request<FuelPage>(`/fuel?${query}`);
      if (sequence === this.loadSequence) this.data.set(result);
    } catch (e) {
      if (sequence === this.loadSequence) this.error.set(this.describe(e));
    } finally {
      if (sequence === this.loadSequence) this.loading.set(false);
    }
  }
  filter() {
    this.page.set(1);
    void this.load();
  }
  changePage(delta: number) {
    this.page.set(this.page() + delta);
    void this.load();
  }
  selectVehicle() {
    const vehicle = this.vehicles().find((v) => v.id === this.vehicleId);
    this.odometer = vehicle?.odometer ?? 0;
    this.vehicleVersion = vehicle?.version ?? null;
  }
  open() {
    this.vehicleId = 0;
    this.date = this.today();
    this.odometer = 0;
    this.fuelType = "Gasolina";
    this.liters = null;
    this.unitPrice = null;
    this.station = "";
    this.reference = "";
    this.fullTank = false;
    this.vehicleVersion = null;
    this.requestId = crypto.randomUUID();
    this.modalError.set("");
    this.editor.nativeElement.showModal();
  }
  close() {
    if (!this.saving()) this.editor.nativeElement.close();
  }
  async save() {
    if (this.saving()) return;
    this.saving.set(true);
    this.modalError.set("");
    try {
      await this.api.request("/fuel", "POST", {
        vehicleId: this.vehicleId,
        date: this.date,
        odometer: this.odometer,
        fuelType: this.fuelType,
        liters: this.liters,
        unitPrice: this.unitPrice,
        station: this.station,
        reference: this.reference,
        fullTank: this.fullTank,
        requestId: this.requestId,
        vehicleVersion: this.vehicleVersion,
      });
      this.editor.nativeElement.close();
      this.saved.emit("Abastecimento registrado e hodômetro atualizado.");
      this.page.set(1);
      await this.load();
    } catch (e) {
      this.modalError.set(this.describe(e));
    } finally {
      this.saving.set(false);
    }
  }
  describe(e: unknown) {
    return e instanceof Error
      ? e.message
      : "Não foi possível concluir a operação.";
  }
}
