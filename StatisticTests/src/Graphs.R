library(ggplot2)
library(dplyr)
library(forcats)
library(tidyr)
library(vars)
library(broom)
library(knitr)

Sys.setenv(JAVA_HOME='')

CreateConnection <- function() {
  return(odbc::dbConnect(RPostgres::Postgres(),
                         dbname = "stock", host = "localhost",
                         port = 5432, user = "postgres", password = "postgres"
  ))
}

GetSectors <- function(conn) {
  return(odbc::dbGetQuery(conn, 'SELECT * FROM "Sector"'))
}

ExportVARModelTable <- function(model, sectors) {
  sector_name <- model$sector
  if(is.numeric(model$sector)) {
    sector_name <- sectors %>% filter(Id == model$sector) %>% .$Name
  }
  sink(paste("models/Tables/sector_", sector_name, ".txt", sep = ""))
  print(summary(model$var_model$model))
  sink()
}

ExportVARImpact <- function(model, sectors) {
  sector_name <- model$sector
  if(is.numeric(model$sector)) {
    sector_name <- sectors %>% filter(Id == model$sector) %>% .$Name
  }
  assign("optimal_lag", model$var_model$optimal_lag, envir = .GlobalEnv)
  impulse <- vars::irf(model$var_model$model, impulse = "Rate", response = c("SectorVariance"),
                       n.ahead = 10,ortho = T, cumulative = F, boot = T)
  impulse$Upper$Rate <- impulse$Upper$Rate * 252
  impulse$Lower$Rate <- impulse$Lower$Rate * 252
  impulse$irf$Rate <- impulse$irf$Rate * 252
  df <- data.frame(Days = 1:11, Lower = impulse$Lower$Rate, IRF = impulse$irf$Rate, Upper = impulse$Upper$Rate)
  colnames(df) <- c("Days","Lower", "IRF", "Upper")
  xlsx::write.xlsx(df,"models/Tables/impact.xlsx", sheetName = sector_name, row.names = F, append = T)
}

# Carregar dados
conn <- CreateConnection()
model <- readRDS("models/sector_10.rds")
rates <- readRDS("models/rates.rds")
us_rates <- readRDS("models/usa_rates.rds")
sectors <- GetSectors(conn) %>% arrange(Id)
odbc::dbDisconnect(conn)

# Preparar os dados
data <- model$data$data %>%
  inner_join(rates, by = "Date") %>%
  mutate(MarketVolatility = MarketVolatility * sqrt(252)) %>%
  dplyr::select(Date, Juros = Rate, Volatilidade = MarketVolatility)

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
market_model <- readRDS("models/market.rds")

# Exportar tabelas
if(!dir.exists("models/Tables")) {
  dir.create("models/Tables")
}
lapply(all_models, ExportVARModelTable, sectors)
ExportVARModelTable(market_model, sectors)
# Remove xlsx
if (file.exists("models/Tables/impact.xlsx")) {
  file.remove("models/Tables/impact.xlsx")
}
lapply(all_models, ExportVARImpact, sectors)
ExportVARImpact(market_model, sectors)

uncvariances <- sapply(all_models, \(x) mean(x$data$data$NonAdjusted^2)*252)
uncvariances_adjusted <- sapply(all_models, \(x) mean(x$data$data$SectorVariance, na.rm = F)*252)
sectors$unconditional_variance <- uncvariances
sectors$unconditional_variance_adjusted <- uncvariances_adjusted
sectors %>%
  dplyr::select(Name, Volatilidade = unconditional_variance, VolatilidadeAjustada = unconditional_variance_adjusted) %>%
  arrange(Volatilidade) %>%
  pivot_longer(c(Volatilidade, VolatilidadeAjustada), names_to = "VarType") %>%
  mutate(value = sqrt(value)) %>%
  ggplot(aes(x = value, fill = fct_relevel(VarType, "VolatilidadeAjustada", "Volatilidade"), y = fct_inorder(Name))) +
  geom_bar(stat = "identity", position = position_dodge()) +
  scale_x_continuous(
    labels = scales::percent,
    n.breaks = 10,
  )+
  labs(x = "Volatilidade Incondicional", y = "", title = "Volatilidade por Setor",
       subtitle = "Volatilidade anualizada", fill = "") +
  theme_minimal()+
  theme(text = element_text(size = 10))+
  scale_fill_manual(values = c("Volatilidade" = "#ff7f00", "VolatilidadeAjustada" = "#377eb8"))

ggsave("images/VarUnconditional.png", bg = "white", width = 1920, height = 1080, units = "px")

model$data$data %>%
  filter(Date >= '2010-01-01' & Date <= '2020-01-01') %>%
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

market_model$data$data %>%
  filter(Date >= '2015-01-01' & Date <= '2020-01-01') %>%
  inner_join(us_rates, by = "Date") %>%
  ggplot(aes(x = Date))+
  geom_line(aes(y = MarketVolatility*sqrt(252), col = "Volatilidade Mercado"), linewidth = 0.8, lty = 1)+
  geom_line(aes(y = UsaRate*25, col = "Juros EUA"), linewidth = 0.8, lty = 2)+
  theme_minimal()+
  theme(legend.position = "bottom")+
  scale_color_manual(values = c("Volatilidade Mercado" = "#ff7f00",
                                "Juros EUA" = "#377eb8"))+
  scale_y_continuous(labels = scales::percent, n.breaks = 10,
                     sec.axis = sec_axis(~ ./25, name = "Juros EUA", labels = scales::percent))+
  labs(x = "", y = "",
       title = "Volatilidade e juros EUA",
       col = "")

ggsave("images/MarketImpact.png", bg = "white")

teste <- model$data$data %>%
  dplyr::inner_join(model$real_exchange, by = "Date")

modelo <- lm(MarketReturn ~ RealExchangeRate, data = teste)

summary(modelo)

lm_eqn <- function(m){
  eq <- substitute(italic(y) == a + b %.% italic(x)*","~~italic(r)^2~"="~r2, 
                   list(a = format(unname(coef(m)[1]), digits = 2),
                        b = format(unname(coef(m)[2]), digits = 2),
                        r2 = format(summary(m)$r.squared, digits = 3)))
  as.character(as.expression(eq));
}

teste %>%
  ggplot()+
  geom_point(aes(x = RealExchangeRate, y = MarketReturn), size = 0.9)+
  geom_smooth(aes(x = RealExchangeRate, y = MarketReturn), method = "lm", se = T, col = "red")+
  theme_minimal()+
  theme(legend.position = "bottom")+
  scale_y_continuous(labels = scales::percent, n.breaks = 10)+
  scale_x_continuous(labels = scales::percent, n.breaks = 10)+
  labs(x = "Taxa de Câmbio Real", y = "Retorno de Mercado",
       title = "Câmbio e Mercado",
       col = "")

ggsave("images/MarketReturn.png", bg = "white")
