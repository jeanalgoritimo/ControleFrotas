import { registerLocaleData } from "@angular/common";
import pt from "@angular/common/locales/pt";
registerLocaleData(pt, "pt-BR");
import { bootstrapApplication } from "@angular/platform-browser";
import { App } from "./app";
bootstrapApplication(App).catch(console.error);
