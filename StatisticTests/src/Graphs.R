library(ggplot2)
library(dplyr)
library(forcats)
library(tidyr)

CreateConnection <- function() {
  return(odbc::dbConnect(RPostgres::Postgres(),
                         dbname = "stock", host = "localhost",
                         port = 5432, user = "postgres", password = "postgres"
  ))
}

GetSectors <- function(conn) {
  return(odbc::dbGetQuery(conn, 'SELECT * FROM "Sector"'))
}

# Carregar dados
conn <- CreateConnection()
model <- readRDS("models/sector_11.rds")
rates <- readRDS("models/rates.rds")
sectors <- GetSectors(conn) %>% arrange(Id)
odbc::dbDisconnect(conn)

# Preparar os dados
data <- model$data$data %>%
  inner_join(rates, by = "Date") %>%
  mutate(MarketVolatility = MarketVolatility * sqrt(252)) %>%
  select(Date, Juros = Rate, Volatilidade = MarketVolatility)

# Criar o gráfico
ggplot(data, aes(x = Date)) +
  geom_line(aes(y = Juros, colour = "Juros"), linewidth = 0.8) +  # Linha para Juros
  geom_line(aes(y = Volatilidade/5, colour = "Volatilidade"), linewidth = 0.8) +  # Linha para Volatilidade ajustada
  scale_y_continuous(
    name = "Juros",
    labels = scales::percent,
    sec.axis = sec_axis(~ . * 5, name = "Volatilidade", labels = scales::percent),
    n.breaks = 10,
  ) +
  labs(colour = "", x = "", title = "Volatilidade do Mercado e Juros ETTJ",
       subtitle = "Volatilidade anualizada e ETTJ Vértice 1 ano") +
  theme_minimal()+
  theme(legend.position = "bottom", text = element_text(size = 20),
        axis.title.y = element_text(size = 15), title = element_text(size = 15))+
  scale_colour_manual(values = c("Juros" = "#377eb8", "Volatilidade" = "#ff7f00"))

ggsave("images/VolMercado.png", bg = "white", create.dir = T)

all_models <- lapply(1:15, \(x) readRDS(paste0("models/sector_", x, ".rds")))
uncvariances <- sapply(all_models, \(x) mean(x$data$data$NonAdjusted^2)*252)
uncvariances_adjusted <- sapply(all_models, \(x) mean(x$data$data$SectorVariance, na.rm = T)*252)
sectors$unconditional_variance <- uncvariances
sectors$unconditional_variance_adjusted <- uncvariances_adjusted
sectors %>%
  select(Name, Variancia = unconditional_variance, VarianciaAjustada = unconditional_variance_adjusted) %>%
  arrange(Variancia) %>%
  pivot_longer(c(Variancia, VarianciaAjustada), names_to = "VarType") %>%
  ggplot(aes(x = value, fill = fct_relevel(VarType, "VarianciaAjustada", "Variancia"), y = fct_inorder(Name))) +
  geom_bar(stat = "identity", position = position_dodge()) +
  scale_x_continuous(
    labels = scales::percent,
    n.breaks = 10,
  )+
  labs(x = "Variância Incondicional", y = "", title = "Variâncias por Setor",
       subtitle = "Variância anualizada", fill = "") +
  theme_minimal()+
  theme(text = element_text(size = 20))+
  scale_fill_manual(values = c("Variancia" = "#ff7f00", "VarianciaAjustada" = "#377eb8"))

ggsave("images/VarUnconditional.png", bg = "white")

model$data$data %>%
  filter(Date >= '2015-01-01' & Date <= '2020-01-01') %>%
  ggplot(aes(x = Date))+
  geom_line(aes(y = SectorVolatility*sqrt(252), col = "Volatilidade Ajustada"), linewidth = 0.8, lty = 1)+
  geom_line(aes(y = NonAdjusted*sqrt(252), col = "Volatilidade Nominal"), linewidth = 0.8, lty = 2)+
  geom_line(aes(y = MarketImpact*sqrt(252), col = "Impacto do Mercado"), linewidth = 0.8, lty = 3)+
  theme_minimal()+
  theme(legend.position = "bottom")+
  scale_color_manual(values = c("Volatilidade Ajustada" = "#ff7f00",
                                "Volatilidade Nominal" = "#377eb8",
                                "Impacto do Mercado" = "#00BF4E"))+
  scale_y_continuous(labels = scales::percent, n.breaks = 10)+
  labs(x = "", y = "",
       subtitle = "Volatilidade anualizada",
       title = paste("Volatilidades do setor", sectors %>% filter(Id == model$sector) %>% .$Name),
       col = "")

ggsave("images/AdjustedVol.png", bg = "white")
