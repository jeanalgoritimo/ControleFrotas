import { Component, input, output } from "@angular/core";
import { FormsModule } from "@angular/forms";
@Component({
  selector: "fleet-login",
  imports: [FormsModule],
  templateUrl: "./login.html",
})
export class Login {
  loading = input(false);
  error = input("");
  credentials = output<{ username: string; password: string }>();
  username = "";
  password = "";
  submit() {
    this.credentials.emit({ username: this.username, password: this.password });
    this.password = "";
  }
}
