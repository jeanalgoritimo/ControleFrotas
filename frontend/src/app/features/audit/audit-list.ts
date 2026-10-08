import { Component, input } from "@angular/core";
import { DatePipe } from "@angular/common";
import { Audit } from "../fleet/fleet.models";
@Component({
  selector: "fleet-audit-list",
  imports: [DatePipe],
  templateUrl: "./audit-list.html",
})
export class AuditList {
  audits = input<Audit[]>([]);
}
