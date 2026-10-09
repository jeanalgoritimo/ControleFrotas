import {
  Directive,
  ElementRef,
  forwardRef,
  HostListener,
  inject,
} from "@angular/core";
import {
  AbstractControl,
  ControlValueAccessor,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  ValidationErrors,
  Validator,
} from "@angular/forms";
import { formatPlate, normalizePlate, validPlate } from "./brazilian-plate";

@Directive({
  selector: "input[fleetPlate]",
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => BrazilianPlateDirective),
      multi: true,
    },
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => BrazilianPlateDirective),
      multi: true,
    },
  ],
})
export class BrazilianPlateDirective
  implements ControlValueAccessor, Validator
{
  private input = inject<ElementRef<HTMLInputElement>>(ElementRef);
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};
  writeValue(value: string | null): void {
    this.input.nativeElement.value = formatPlate(value ?? "");
  }
  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(disabled: boolean): void {
    this.input.nativeElement.disabled = disabled;
  }
  validate(control: AbstractControl): ValidationErrors | null {
    return !control.value || validPlate(control.value)
      ? null
      : { brazilianPlate: true };
  }
  @HostListener("blur") blur(): void {
    this.onTouched();
  }
  @HostListener("input") inputChanged(): void {
    const field = this.input.nativeElement;
    const raw = field.value;
    const start = field.selectionStart ?? raw.length;
    const end = field.selectionEnd ?? start;
    const formatted = formatPlate(raw);
    field.value = formatted;
    // Map positions by registration characters, so adding/removing the dash
    // does not move the cursor to the end during an edit.
    const position = (offset: number): number => {
      const count = raw.slice(0, offset).replace(/-/g, "").length;
      return Math.min(
        formatted.length,
        count + (formatted[3] === "-" && count > 3 ? 1 : 0),
      );
    };
    field.setSelectionRange(position(start), position(end));
    this.onChange(normalizePlate(formatted));
  }
  @HostListener("paste", ["$event"]) paste(event: ClipboardEvent): void {
    const text = event.clipboardData?.getData("text");
    if (text === undefined) return;
    const field = this.input.nativeElement;
    const start = field.selectionStart ?? field.value.length;
    const end = field.selectionEnd ?? start;
    const candidate =
      field.value.slice(0, start) + text.trim() + field.value.slice(end);
    // Do not silently truncate a pasted registration into a different valid one.
    event.preventDefault();
    field.value = candidate;
    field.setSelectionRange(
      start + text.trim().length,
      start + text.trim().length,
    );
    this.inputChanged();
  }
}
