import type { Metadata } from "next";
import "./globals.css";
export const metadata: Metadata = { title: "Revenda Pro", description: "Gestão para revendas" };
export default function RootLayout({ children }: { children: React.ReactNode }) { return <html lang="pt-BR"><body>{children}</body></html>; }
