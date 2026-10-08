import { Component, ElementRef, ViewChild, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

interface Vehicle { id: number; plate: string; category: string; brand: string; model: string; year: number; odometer: number; active: boolean; version: string | null; }
interface Driver { id: number; name: string; cpf: string; license: string; licenseCategory: string; licenseExpiry: string; active: boolean; version: string | null; }
interface Audit { id: number; action: string; actor: string; recordId: number; atUtc: string; }
@Component({ selector: 'app-root', standalone: true, imports: [CommonModule, FormsModule], templateUrl: './app.html' })
export class App {
  @ViewChild('editor') editor!: ElementRef<HTMLDialogElement>;
  user = signal(''); loading = signal(false); message = signal(''); error = signal(''); modalError = signal('');
  vehicles = signal<Vehicle[]>([]); drivers = signal<Driver[]>([]); audits = signal<Audit[]>([]);
  tab = signal('overview'); query = signal(''); onlyActive = signal(true); page = signal(1);
  username = ''; password = ''; kind = 'vehicle'; saving = signal(false); csrf = '';
  vehicle = this.emptyVehicle(); driver = this.emptyDriver();
  vehicleRows = computed(() => this.vehicles().filter(v => (!this.onlyActive() || v.active) && `${v.plate} ${v.brand} ${v.model}`.toLowerCase().includes(this.query().toLowerCase())));
  driverRows = computed(() => this.drivers().filter(d => (!this.onlyActive() || d.active) && d.name.toLowerCase().includes(this.query().toLowerCase())));
  rowsCount = computed(() => this.tab() === 'vehicles' ? this.vehicleRows().length : this.driverRows().length);
  pages = computed(() => Math.max(1, Math.ceil(this.rowsCount() / 10)));
  constructor() { void this.restore(); }
  emptyVehicle(): Vehicle { return { id: 0, plate: '', category: 'Carro', brand: '', model: '', year: new Date().getFullYear(), odometer: 0, active: true, version: null }; }
  emptyDriver(): Driver { return { id: 0, name: '', cpf: '', license: '', licenseCategory: 'B', licenseExpiry: '', active: true, version: null }; }
  async request<T>(url: string, method = 'GET', body?: unknown): Promise<T> {
    const response = await fetch(`/api${url}`, { method, credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': this.csrf }, body: body === undefined ? undefined : JSON.stringify(body) });
    if (!response.ok) {
      if (response.status === 401 && url !== '/auth/login') this.user.set('');
      const data = await response.json().catch(() => ({ message: response.status === 429 ? 'Muitas tentativas. Aguarde um minuto.' : 'Falha na solicitação.' }));
      throw new Error(data.message ?? 'Não foi possível concluir a operação.');
    }
    return response.status === 204 ? undefined as T : response.json() as Promise<T>;
  }
  async refreshCsrf() { this.csrf = (await this.request<{token: string}>('/auth/csrf')).token; }
  async restore() {
    try { const me = await this.request<{name: string}>('/auth/me'); this.user.set(me.name); await this.refreshCsrf(); await this.load(); }
    catch { this.user.set(''); }
  }
  async login() {
    this.loading.set(true); this.error.set('');
    try { await this.refreshCsrf(); const me = await this.request<{name: string}>('/auth/login', 'POST', { username: this.username, password: this.password }); this.password = ''; this.user.set(me.name); await this.refreshCsrf(); await this.load(); }
    catch (e) { this.error.set(this.describe(e)); } finally { this.loading.set(false); }
  }
  async logout() { try { await this.request('/auth/logout', 'POST'); this.user.set(''); this.vehicles.set([]); this.drivers.set([]); this.audits.set([]); this.csrf = ''; } catch (e) { this.error.set(this.describe(e)); } }
  async load() {
    this.loading.set(true); this.error.set('');
    try { const [vehicles, drivers, audits] = await Promise.all([this.request<Vehicle[]>('/vehicles'), this.request<Driver[]>('/drivers'), this.request<Audit[]>('/audit')]); this.vehicles.set(vehicles); this.drivers.set(drivers); this.audits.set(audits); }
    catch (e) { this.error.set(this.describe(e)); } finally { this.loading.set(false); }
  }
  navigate(tab: string) { this.tab.set(tab); this.query.set(''); this.page.set(1); this.message.set(''); }
  openVehicle(v?: Vehicle) { this.kind = 'vehicle'; this.vehicle = v ? { ...v } : this.emptyVehicle(); this.showEditor(); }
  openDriver(d?: Driver) { this.kind = 'driver'; this.driver = d ? { ...d } : this.emptyDriver(); this.showEditor(); }
  showEditor() { this.modalError.set(''); this.editor.nativeElement.showModal(); }
  closeEditor() { if (!this.saving()) this.editor.nativeElement.close(); }
  async save() {
    if (this.saving()) return;
    this.saving.set(true); this.modalError.set('');
    try {
      const record = this.kind === 'vehicle' ? this.vehicle : this.driver;
      const path = this.kind === 'vehicle' ? '/vehicles' : '/drivers';
      await this.request(path + (record.id ? `/${record.id}` : ''), record.id ? 'PUT' : 'POST', record);
      this.editor.nativeElement.close(); this.message.set('Cadastro salvo com sucesso.'); await this.load();
    } catch (e) { this.modalError.set(this.describe(e)); } finally { this.saving.set(false); }
  }
  number(value: number) { return value.toLocaleString('pt-BR', { maximumFractionDigits: 3 }); }
  expired(d: Driver) { return d.licenseExpiry < new Date().toLocaleDateString('sv-SE'); }
  describe(e: unknown) { return e instanceof Error ? e.message : 'Não foi possível conectar à API. Confira se ela está iniciada.'; }
}
