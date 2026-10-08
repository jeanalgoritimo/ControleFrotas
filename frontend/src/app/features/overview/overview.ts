import { Component, input, output } from "@angular/core";
@Component({ selector: "fleet-overview", templateUrl: "./overview.html" })
export class Overview {
  vehicleCount = input(0);
  driverCount = input(0);
  auditCount = input(0);
  navigate = output<string>();
}
