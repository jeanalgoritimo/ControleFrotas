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
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Vehicle, Driver } from "./fleet.models";
import { ApiClient } from "../../core/api-client";
@Component({
  selector: "fleet-workspace",
  imports: [CommonModule, FormsModule],
  templateUrl: "./fleet-workspace.html",
})
export class FleetWorkspace {
  @ViewChild("editor") editor!: ElementRef<HTMLDialogElement>;
  tab = input("vehicles");
  vehicles = input<Vehicle[]>([]);
  drivers = input<Driver[]>([]);
  saved = output<string>();
  query = signal("");
  onlyActive = signal(true);
  page = signal(1);
  modalError = signal("");
  saving = signal(false);
  kind = "vehicle";
  api = inject(ApiClient);
  vehicle = this.emptyVehicle();
  driver = this.emptyDriver();
  vehicleRows = computed(() =>
    this.vehicles().filter(
      (v) =>
        (!this.onlyActive() || v.active) &&
        `${v.plate} ${v.brand} ${v.model}`
          .toLowerCase()
          .includes(this.query().toLowerCase()),
    ),
  );
  driverRows = computed(() =>
    this.drivers().filter(
      (d) =>
        (!this.onlyActive() || d.active) &&
        d.name.toLowerCase().includes(this.query().toLowerCase()),
    ),
  );
  rowsCount = computed(() =>
    this.tab() === "vehicles"
      ? this.vehicleRows().length
      : this.driverRows().length,
  );
  pages = computed(() => Math.max(1, Math.ceil(this.rowsCount() / 10)));
  constructor() {
    effect(() => {
      this.tab();
      this.query.set("");
      this.page.set(1);
    });
  }
  emptyVehicle(): Vehicle {
    return {
      id: 0,
      plate: "",
      category: "Carro",
      brand: "",
      model: "",
      year: new Date().getFullYear(),
      odometer: 0,
      active: true,
      version: null,
    };
  }
  emptyDriver(): Driver {
    return {
      id: 0,
      name: "",
      cpf: "",
      license: "",
      licenseCategory: "B",
      licenseExpiry: "",
      active: true,
      version: null,
    };
  }
  openVehicle(v?: Vehicle) {
    this.kind = "vehicle";
    this.vehicle = v ? { ...v } : this.emptyVehicle();
    this.showEditor();
  }
  openDriver(d?: Driver) {
    this.kind = "driver";
    this.driver = d ? { ...d } : this.emptyDriver();
    this.showEditor();
  }
  showEditor() {
    this.modalError.set("");
    this.editor.nativeElement.showModal();
  }
  closeEditor() {
    if (!this.saving()) this.editor.nativeElement.close();
  }
  async save() {
    if (this.saving()) return;
    this.saving.set(true);
    this.modalError.set("");
    try {
      const record = this.kind === "vehicle" ? this.vehicle : this.driver;
      const path = this.kind === "vehicle" ? "/vehicles" : "/drivers";
      await this.api.request(
        path + (record.id ? `/${record.id}` : ""),
        record.id ? "PUT" : "POST",
        record,
      );
      this.editor.nativeElement.close();
      this.saved.emit("Cadastro salvo com sucesso.");
    } catch (e) {
      this.modalError.set(this.describe(e));
    } finally {
      this.saving.set(false);
    }
  }
  number(value: number) {
    return value.toLocaleString("pt-BR", { maximumFractionDigits: 3 });
  }
  expired(d: Driver) {
    return d.licenseExpiry < new Date().toLocaleDateString("sv-SE");
  }
  describe(e: unknown) {
    return e instanceof Error
      ? e.message
      : "Não foi possível conectar à API. Confira se ela está iniciada.";
  }
}
