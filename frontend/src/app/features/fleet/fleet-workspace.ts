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
import { VehicleBrand, VehicleModel } from "../catalog/catalog.models";
import { BrazilianPlateDirective } from "./brazilian-plate.directive";
import { Vehicle, Driver } from "./fleet.models";
import { ApiClient } from "../../core/api-client";
@Component({
  selector: "fleet-workspace",
  imports: [CommonModule, FormsModule, BrazilianPlateDirective],
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
  brands = signal<VehicleBrand[]>([]);
  models = signal<VehicleModel[]>([]);
  catalogLoading = signal(false);
  modelsLoading = signal(false);
  catalogFailed = signal(false);
  private catalogSequence = 0;
  private modelsSequence = 0;
  private originalBrandId = 0;
  private originalModelId = 0;
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
      brandId: 0,
      modelId: 0,
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
    this.originalBrandId = v?.brandId ?? 0;
    this.originalModelId = v?.modelId ?? 0;
    this.brands.set([]);
    this.models.set([]);
    this.showEditor();
    void this.loadCatalog();
  }
  async loadCatalog() {
    const sequence = ++this.catalogSequence;
    this.catalogLoading.set(true);
    this.catalogFailed.set(false);
    this.modalError.set("");
    try {
      const brands = await this.api.request<VehicleBrand[]>(
        "/vehicle-brands?includeInactive=true",
      );
      if (sequence !== this.catalogSequence) return;
      this.brands.set(
        brands.filter((b) => b.active || b.id === this.originalBrandId),
      );
      await this.loadModels(this.vehicle.brandId, this.vehicle.modelId);
    } catch (e) {
      if (sequence === this.catalogSequence) {
        this.catalogFailed.set(true);
        this.modalError.set(this.describe(e));
      }
    } finally {
      if (sequence === this.catalogSequence) this.catalogLoading.set(false);
    }
  }
  async changeBrand(brandId: number) {
    this.vehicle.brandId = brandId;
    this.vehicle.modelId = 0;
    this.vehicle.model = "";
    this.vehicle.brand =
      this.brands().find((b) => b.id === brandId)?.name ?? "";
    await this.loadModels(brandId, 0);
  }
  async loadModels(brandId: number, selectedModel: number) {
    const sequence = ++this.modelsSequence;
    this.models.set([]);
    this.modelsLoading.set(brandId > 0);
    this.catalogFailed.set(false);
    if (!brandId) return;
    try {
      const models = await this.api.request<VehicleModel[]>(
        `/vehicle-models?brandId=${brandId}&includeInactive=true`,
      );
      if (sequence !== this.modelsSequence) return;
      this.models.set(
        models.filter(
          (m) =>
            (m.active &&
              !!this.brands().find((b) => b.id === brandId)?.active) ||
            (brandId === this.originalBrandId && m.id === this.originalModelId),
        ),
      );
      this.vehicle.modelId = this.models().some((m) => m.id === selectedModel)
        ? selectedModel
        : 0;
    } catch (e) {
      if (sequence === this.modelsSequence) {
        this.catalogFailed.set(true);
        this.modalError.set(this.describe(e));
      }
    } finally {
      if (sequence === this.modelsSequence) this.modelsLoading.set(false);
    }
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
  invalidateCatalog() {
    ++this.catalogSequence;
    ++this.modelsSequence;
  }
  async save() {
    if (this.saving()) return;
    if (
      this.kind === "vehicle" &&
      (!this.vehicle.brandId ||
        !this.vehicle.modelId ||
        this.catalogLoading() ||
        this.modelsLoading() ||
        this.catalogFailed())
    ) {
      this.modalError.set("Selecione uma marca e um modelo cadastrados.");
      return;
    }
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
