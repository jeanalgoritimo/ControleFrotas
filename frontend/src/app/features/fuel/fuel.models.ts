export interface FuelEntry {
  id: number;
  vehicleId: number;
  plate: string;
  date: string;
  odometer: number;
  fuelType: string;
  liters: number;
  unitPrice: number;
  total: number;
  station: string;
  reference: string;
  fullTank: boolean;
  kmPerLiter: number | null;
}
export interface FuelPage {
  items: FuelEntry[];
  totalCount: number;
  totalLiters: number;
  totalCost: number;
}
