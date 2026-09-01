import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Revenda Pro",
  description: "Gestão para revendas de veículos",
};

/**
* Tema e estado da barra lateral aplicados antes da hidratacao: evita piscar claro
 * para quem usa escuro e evita o scrim do modal nascer com a largura errada.
 */
const applyTheme = `
(function(){try{
  var root=document.documentElement;
  if(localStorage.getItem("revenda-pro-theme")==="dark"){root.classList.add("dark");}
  root.dataset.sidebar=localStorage.getItem("revenda-pro-sidebar")==="1"?"collapsed":"expanded";
}catch(e){}})();
`;

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR">
      <head>
        <script dangerouslySetInnerHTML={{ __html: applyTheme }} />
      </head>
      <body>{children}</body>
    </html>
  );
}
