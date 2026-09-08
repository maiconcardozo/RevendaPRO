import type { Metadata, Viewport } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Revenda Pro",
  description: "Gestão para revendas de veículos",
  applicationName: "Revenda Pro",
  // Na tela inicial do iPhone (M20): abre sem a barra do Safari, com o nome curto embaixo do ícone.
  appleWebApp: { capable: true, title: "Revenda Pro", statusBarStyle: "default" },
};

/**
 * A cor da barra do sistema no celular (theme-color) fica fora daqui de propósito: o tema é
 * o que a pessoa escolheu no botão, guardado no localStorage, e o script abaixo escreve a meta
 * tag com a cor certa antes da primeira pintura. O PanelShell a reescreve a cada troca.
 */
export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  viewportFit: "cover",
};

/**
* Tema e estado da barra lateral aplicados antes da hidratacao: evita piscar claro
 * para quem usa escuro e evita o scrim do modal nascer com a largura errada.
 */
const applyTheme = `
(function(){try{
  var root=document.documentElement;
  var dark=localStorage.getItem("revenda-pro-theme")==="dark";
  if(dark){root.classList.add("dark");}
  var m=document.querySelector("meta[name=theme-color]");
  if(!m){m=document.createElement("meta");m.name="theme-color";document.head.appendChild(m);}
  m.content=dark?"#1a1c1f":"#edf1f4";
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
