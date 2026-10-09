export interface VehicleBrand {
  id: number;
  name: string;
  active: boolean;
  version: string | null;
}
export interface VehicleModel extends VehicleBrand {
  brandId: number;
}
