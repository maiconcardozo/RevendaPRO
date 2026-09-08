import type { MetadataRoute } from "next";

/**
 * O que a tela inicial do celular precisa saber (M20): nome, ícone e que abre sem a barra do
 * navegador. Sem service worker, de propósito: o sistema precisa da rede, e um cache que
 * mostrasse um preço velho seria pior do que a tela avisando que o servidor está fora.
 */
export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Revenda Pro",
    short_name: "Revenda Pro",
    description: "Gestão para revendas de veículos",
    start_url: "/dashboard",
    display: "standalone",
    orientation: "portrait",
    lang: "pt-BR",
    background_color: "#edf1f4",
    theme_color: "#0090c4",
    icons: [
      { src: "/icons/icon-192.png", sizes: "192x192", type: "image/png", purpose: "any" },
      { src: "/icons/icon-512.png", sizes: "512x512", type: "image/png", purpose: "any" },
      { src: "/icons/icon-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
    ],
  };
}
