<!-- eslint-disable vue/multi-word-component-names -->
<template>
  <form id="myDropdown" class="form center" @submit.prevent="send">
    <h3 class="form__title">Application form</h3>
    <input
      v-model.trim="form.name"
      placeholder="Name"
      required
      class="form__input"
      type="text"
      name="name"
      autocomplete="name"
    />
    <input
      v-model.trim="form.phoneNumber"
      placeholder="Phone"
      required
      class="form__input"
      type="tel"
      name="phoneNumber"
      autocomplete="tel"
    />
    <input
      v-model.trim="form.email"
      placeholder="E-mail"
      required
      class="form__input"
      type="email"
      name="email"
      autocomplete="email"
    />
    <button type="submit" class="form__button" :disabled="sending">
      {{ sending ? "Sending..." : "Send" }}
    </button>
    <p v-if="message" class="form__message" role="status">{{ message }}</p>
  </form>
</template>

<script>
import axios from "axios";

const emptyForm = () => ({ name: "", phoneNumber: "", email: "" });

export default {
  data() {
    return {
      form: emptyForm(),
      sending: false,
      message: "",
    };
  },
  methods: {
    async send() {
      this.sending = true;
      this.message = "";
      try {
        await axios.post("/api/Applications", this.form);
        this.form = emptyForm();
        this.message = "Thank you! We will contact you soon.";
      } catch (error) {
        const errors = error.response?.data?.errors;
        this.message = errors
          ? Object.values(errors).flat().join(" ")
          : "Something went wrong. Please try again later.";
      } finally {
        this.sending = false;
      }
    },
  },
};
</script>
