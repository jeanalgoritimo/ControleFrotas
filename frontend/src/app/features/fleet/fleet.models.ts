export interface Vehicle {
  id: number;
  plate: string;
  category: string;
  brand: string;
  model: string;
  year: number;
  odometer: number;
  active: boolean;
  version: string | null;
}
export interface Driver {
  id: number;
  name: string;
  cpf: string;
  license: string;
  licenseCategory: string;
  licenseExpiry: string;
  active: boolean;
  version: string | null;
}
export interface Audit {
  id: number;
  action: string;
  actor: string;
  recordId: number;
  atUtc: string;
}
