// CONTRAN 969/2022, art. 2º §3º and Annex I item 1.2.
// Formatting never converts an old registration into a Mercosur registration.
export function normalizePlate(value: string): string {
  const upper = value.trim().replace(/[a-z]/g, (c) => c.toUpperCase());
  return /^[A-Z]{3}-[0-9][A-Z0-9]{0,3}$/.test(upper)
    ? upper.slice(0, 3) + upper.slice(4)
    : upper;
}
export function formatPlate(value: string): string {
  const plate = normalizePlate(value);
  return /^[A-Z]{3}[0-9]{2,4}$/.test(plate)
    ? plate.slice(0, 3) + "-" + plate.slice(3)
    : plate;
}
export function validPlate(value: string): boolean {
  return /^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$/.test(normalizePlate(value));
}
