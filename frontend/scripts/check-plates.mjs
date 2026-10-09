import assert from "node:assert/strict";
import { readFile, writeFile, mkdir, rm } from "node:fs/promises";
import { pathToFileURL } from "node:url";
import { resolve } from "node:path";
import ts from "typescript";
import "@angular/compiler";
import { ElementRef, Injector, runInInjectionContext } from "@angular/core";

const temporary = resolve(".angular/plate-checks");
await mkdir(temporary, { recursive: true });
let checks = 0;
const check = (actual, expected) => { assert.deepEqual(actual, expected); checks++; };
try {
  for (const name of ["brazilian-plate", "brazilian-plate.directive"]) {
    const source = await readFile(`src/app/features/fleet/${name}.ts`, "utf8");
    const output = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, experimentalDecorators: true } }).outputText;
    await writeFile(`${temporary}/${name}.mjs`, output.replace('"./brazilian-plate"', '"./brazilian-plate.mjs"'));
  }
  const {formatPlate, normalizePlate, validPlate} = await import(pathToFileURL(`${temporary}/brazilian-plate.mjs`));
  const {BrazilianPlateDirective} = await import(pathToFileURL(`${temporary}/brazilian-plate.directive.mjs`));
  check(formatPlate("abc1234"), "ABC-1234");
  check(formatPlate("abc1d23"), "ABC1D23");
  check(formatPlate(" ABC-1234 "), "ABC-1234");
  check(normalizePlate("abc-1234"), "ABC1234");
  check(normalizePlate("abc1d23"), "ABC1D23");
  for (const value of ["ABC12345", "ABC!1234", "A-BC1234", "ABC--1234", "ABC 1234", "ABC12D3", "ÁBC1234", "aß1234"])
    check(validPlate(value), false);
  const field = {value: "", selectionStart: 0, selectionEnd: 0, disabled: false, setSelectionRange(start, end) { this.selectionStart = start; this.selectionEnd = end; }};
  const injector = Injector.create({providers: [{provide: ElementRef, useValue: new ElementRef(field)}]});
  const directive = runInInjectionContext(injector, () => new BrazilianPlateDirective());
  let stored = "";
  let touched = false;
  directive.registerOnChange(value => { stored = value; });
  directive.registerOnTouched(() => { touched = true; });
  directive.writeValue("ABC1234");
  check(field.value, "ABC-1234");
  function type(value, cursor = value.length) { field.value = value; field.setSelectionRange(cursor, cursor); directive.inputChanged(); }
  type("abc12");
  check([field.value, stored, field.selectionStart], ["ABC-12", "ABC12", 6]);
  type("ABC-1");
  check([field.value, stored, field.selectionStart], ["ABC1", "ABC1", 4]);
  type("ABX-1234", 3);
  check([field.value, stored, field.selectionStart], ["ABX-1234", "ABX1234", 3]);
  type("ABC-1D23", 6);
  check([field.value, stored, field.selectionStart], ["ABC1D23", "ABC1D23", 5]);
  directive.setDisabledState(true); check(field.disabled, true);
  directive.blur(); check(touched, true);
  check(directive.validate({value: "ABC1234"}), null);
  check(directive.validate({value: "ABC1D23"}), null);
  check(directive.validate({value: "ABC123"}), {brazilianPlate: true});
  field.value = "ABC-1234"; field.setSelectionRange(0, field.value.length);
  let prevented = false;
  directive.paste({clipboardData: {getData() { return " abc1d23 "; }}, preventDefault() { prevented = true; }});
  check([field.value, stored, prevented], ["ABC1D23", "ABC1D23", true]);
  field.setSelectionRange(0, field.value.length);
  directive.paste({clipboardData: {getData() { return "ABC12345"; }}, preventDefault() {}});
  check([field.value, stored, directive.validate({value: stored})], ["ABC12345", "ABC12345", {brazilianPlate: true}]);
  injector.destroy();
  console.log(`${checks} verificações da máscara de placas passaram.`);
} finally { await rm(temporary, {recursive: true, force: true}); }
