import {
  Component,
  ElementRef,
  ViewChild,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { ApiClient } from "../../core/api-client";
import { VehicleBrand, VehicleModel } from "./catalog.models";

@Component({
  selector: "fleet-catalog",
  imports: [CommonModule, FormsModule],
  templateUrl: "./catalog-workspace.html",
})
export class CatalogWorkspace {
  @ViewChild("editor") editor!: ElementRef<HTMLDialogElement>;
  refreshKey = input(0);
  saved = output<string>();
  api = inject(ApiClient);
  brands = signal<VehicleBrand[]>([]);
  models = signal<VehicleModel[]>([]);
  selectedBrand = signal(0);
  query = signal("");
  onlyActive = signal(true);
  loading = signal(false);
  modelsLoading = signal(false);
  saving = signal(false);
  error = signal("");
  modalError = signal("");
  kind = "brand";
  record: VehicleModel = {
    id: 0,
    name: "",
    brandId: 0,
    active: true,
    version: null,
  };
  private loadSequence = 0;
  private modelSequence = 0;
  brandRows = computed(() =>
    this.brands().filter(
      (b) =>
        (!this.onlyActive() || b.active) &&
        b.name
          .toLocaleLowerCase("pt-BR")
          .includes(this.query().toLocaleLowerCase("pt-BR")),
    ),
  );
  modelRows = computed(() =>
    this.models().filter((m) => !this.onlyActive() || m.active),
  );
  brandName = computed(
    () => this.brands().find((b) => b.id === this.selectedBrand())?.name ?? "",
  );
  constructor() {
    effect(() => {
      this.refreshKey();
      void this.load();
    });
  }
  async load() {
    const sequence = ++this.loadSequence;
    this.loading.set(true);
    this.error.set("");
    try {
      const brands = await this.api.request<VehicleBrand[]>(
        "/vehicle-brands?includeInactive=true",
      );
      if (sequence !== this.loadSequence) return;
      this.brands.set(brands);
      const current = brands.some((b) => b.id === this.selectedBrand())
        ? this.selectedBrand()
        : 0;
      await this.selectBrand(current);
    } catch (e) {
      if (sequence === this.loadSequence) this.error.set(this.describe(e));
    } finally {
      if (sequence === this.loadSequence) this.loading.set(false);
    }
  }
  async selectBrand(id: number) {
    this.selectedBrand.set(id);
    this.models.set([]);
    this.error.set("");
    const sequence = ++this.modelSequence;
    this.modelsLoading.set(id > 0);
    if (!id) return;
    try {
      const models = await this.api.request<VehicleModel[]>(
        `/vehicle-models?brandId=${id}&includeInactive=true`,
      );
      if (sequence === this.modelSequence) this.models.set(models);
    } catch (e) {
      if (sequence === this.modelSequence) this.error.set(this.describe(e));
    } finally {
      if (sequence === this.modelSequence) this.modelsLoading.set(false);
    }
  }
  openBrand(brand?: VehicleBrand) {
    this.kind = "brand";
    this.record = brand
      ? { ...brand, brandId: 0 }
      : { id: 0, name: "", brandId: 0, active: true, version: null };
    this.openEditor();
  }
  openModel(model?: VehicleModel) {
    this.kind = "model";
    this.record = model
      ? { ...model }
      : {
          id: 0,
          name: "",
          brandId: this.brands().find((b) => b.id === this.selectedBrand())
            ?.active
            ? this.selectedBrand()
            : 0,
          active: true,
          version: null,
        };
    this.openEditor();
  }
  private openEditor() {
    this.modalError.set("");
    this.editor.nativeElement.showModal();
  }
  close() {
    if (!this.saving()) this.editor.nativeElement.close();
  }
  async save() {
    if (this.saving()) return;
    if (
      !this.record.name.trim() ||
      (this.kind === "model" && !this.record.brandId)
    ) {
      this.modalError.set("Preencha o nome e selecione a marca do modelo.");
      return;
    }
    this.saving.set(true);
    this.modalError.set("");
    try {
      const path =
        this.kind === "brand" ? "/vehicle-brands" : "/vehicle-models";
      await this.api.request(
        path + (this.record.id ? `/${this.record.id}` : ""),
        this.record.id ? "PUT" : "POST",
        this.record,
      );
      this.editor.nativeElement.close();
      if (this.kind === "model") this.selectedBrand.set(this.record.brandId);
      this.saved.emit(
        this.kind === "brand"
          ? "Marca salva com sucesso."
          : "Modelo salvo com sucesso.",
      );
    } catch (e) {
      this.modalError.set(this.describe(e));
    } finally {
      this.saving.set(false);
    }
  }
  describe(e: unknown) {
    return e instanceof Error
      ? e.message
      : "Não foi possível carregar os cadastros.";
  }
}
